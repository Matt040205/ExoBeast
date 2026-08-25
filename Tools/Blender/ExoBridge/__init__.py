"""Exo Bridge Blender add-on.

This add-on deliberately exports a narrow, auditable contract. It does not
attempt to translate Blender node graphs or create Unity gameplay data.
"""

bl_info = {
    "name": "Exo Bridge Export",
    "author": "ExoBeast",
    "version": (1, 0, 0),
    "blender": (5, 2, 0),
    "location": "View3D > Sidebar > Exo Bridge",
    "description": "Exports a verified Blender package for Exo Config",
    "category": "Import-Export",
}

import hashlib
import json
import os
import re
import shutil
import tempfile
import uuid
import zipfile
from datetime import datetime, timezone

import bpy
from bpy.props import BoolProperty, EnumProperty, PointerProperty, StringProperty
from bpy.types import Operator, Panel, PropertyGroup


ADDON_VERSION = ".".join(str(value) for value in bl_info["version"])
SCHEMA_VERSION = 1
SUPPORTED_TEXTURE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".tga"}
_SAFE_NAME = re.compile(r"[^A-Za-z0-9._ -]+")


class ExoBridgeError(RuntimeError):
    """A user-correctable package-contract failure."""


def sha256_file(path):
    digest = hashlib.sha256()
    with open(path, "rb") as source:
        for chunk in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def safe_name(value):
    cleaned = _SAFE_NAME.sub("_", value or "").strip(" ._")
    return cleaned or "unnamed"


def relative_path(path):
    return path.replace(os.sep, "/")


def require_supported_image(image, purpose):
    if image is None or image.source != "FILE":
        raise ExoBridgeError(f"{purpose} precisa usar uma Image Texture de arquivo, nao imagem gerada/empacotada.")
    image_path = bpy.path.abspath(image.filepath)
    extension = os.path.splitext(image_path)[1].lower()
    if extension not in SUPPORTED_TEXTURE_EXTENSIONS:
        raise ExoBridgeError(
            f"{purpose} usa '{image_path}', mas so PNG, JPG/JPEG e TGA sao compativeis."
        )
    if not os.path.isfile(image_path):
        raise ExoBridgeError(f"Arquivo de imagem nao encontrado para {purpose}: '{image_path}'.")
    return image_path


def _direct_image_from_socket(socket, material_name):
    if not socket.is_linked or len(socket.links) != 1:
        raise ExoBridgeError(
            f"Material '{material_name}' precisa ligar uma unica Image Texture diretamente ao Base Color."
        )
    node = socket.links[0].from_node
    if node is None or node.type != "TEX_IMAGE":
        raise ExoBridgeError(
            f"Material '{material_name}' usa nodes/procedural no Base Color. O Exo Bridge nao converte shaders Blender."
        )
    return require_supported_image(node.image, f"Base Color do material '{material_name}'")


def inspect_material(material):
    """Returns source images accepted by the Unity ToonExobeasts bridge.

    The complete node graph is intentionally not serialized. A direct base
    image is mandatory; a second Image Texture labelled EXO_SHADING is the
    only optional map exposed by this v1 contract.
    """
    if material is None:
        raise ExoBridgeError("Um Mesh possui slot de material vazio.")
    if not material.use_nodes or material.node_tree is None:
        raise ExoBridgeError(f"Material '{material.name}' nao usa nodes de imagem compativeis.")

    output_nodes = [node for node in material.node_tree.nodes if node.type == "OUTPUT_MATERIAL" and node.is_active_output]
    if len(output_nodes) != 1 or not output_nodes[0].inputs["Surface"].is_linked:
        raise ExoBridgeError(f"Material '{material.name}' precisa ter um Material Output ativo com Surface conectado.")
    shader = output_nodes[0].inputs["Surface"].links[0].from_node
    if shader is None or shader.type != "BSDF_PRINCIPLED":
        raise ExoBridgeError(
            f"Material '{material.name}' nao usa Principled BSDF direto. Shaders procedurais/customizados nao sao exportaveis."
        )

    for input_socket in shader.inputs:
        if input_socket.name != "Base Color" and input_socket.is_linked:
            raise ExoBridgeError(
                f"Material '{material.name}' tem '{input_socket.name}' conectado. Apenas Base Color e EXO_SHADING sao suportados."
            )

    base_path = _direct_image_from_socket(shader.inputs["Base Color"], material.name)
    shading_nodes = [
        node for node in material.node_tree.nodes
        if node.type == "TEX_IMAGE" and (node.label == "EXO_SHADING" or node.name == "EXO_SHADING")
    ]
    if len(shading_nodes) > 1:
        raise ExoBridgeError(f"Material '{material.name}' possui mais de uma Image Texture EXO_SHADING.")
    shading_path = require_supported_image(shading_nodes[0].image, f"EXO_SHADING do material '{material.name}'") if shading_nodes else None
    return base_path, shading_path


def collect_export_objects(root):
    objects = [root] + list(root.children_recursive)
    allowed = {"ARMATURE", "EMPTY", "MESH"}
    result = [obj for obj in objects if obj.type in allowed]
    if not any(obj.type == "MESH" for obj in result):
        raise ExoBridgeError("A raiz escolhida precisa conter pelo menos um Mesh.")
    return result


def collect_material_slots(objects):
    slots = []
    for obj in objects:
        if obj.type != "MESH":
            continue
        for index, slot in enumerate(obj.material_slots):
            material = slot.material
            base_path, shading_path = inspect_material(material)
            slots.append({
                "slotName": f"{obj.name}[{index}]::{material.name}",
                "baseSourcePath": base_path,
                "shadingSourcePath": shading_path,
            })
    if not slots:
        raise ExoBridgeError("Nenhum slot de material foi encontrado nos Meshes exportados.")
    return slots


def selected_actions(objects, export_all_actions):
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    if not armatures:
        return [], None
    if export_all_actions:
        actions = [action for action in bpy.data.actions if action.fcurves]
    else:
        active = armatures[0].animation_data.action if armatures[0].animation_data else None
        actions = [active] if active and active.fcurves else []
    names = set()
    result = []
    for action in actions:
        if action.name not in names:
            names.add(action.name)
            result.append(action)
    return result, armatures[0]


def _capture_selection(context):
    return [obj for obj in context.selected_objects], context.view_layer.objects.active


def _select_only(context, objects, active):
    for obj in context.selected_objects:
        obj.select_set(False)
    for obj in objects:
        obj.select_set(True)
    context.view_layer.objects.active = active


def _restore_selection(context, selection, active):
    for obj in context.selected_objects:
        obj.select_set(False)
    for obj in selection:
        if obj and obj.name in bpy.data.objects:
            obj.select_set(True)
    context.view_layer.objects.active = active if active and active.name in bpy.data.objects else None


def export_fbx(context, objects, active, output_path, action=None):
    previous_selection, previous_active = _capture_selection(context)
    previous_action = None
    try:
        _select_only(context, objects, active)
        if action is not None:
            if active.type != "ARMATURE":
                raise ExoBridgeError("A Action precisa de uma Armature raiz para ser exportada.")
            animation_data = active.animation_data_create()
            previous_action = animation_data.action
            animation_data.action = action
        bpy.ops.export_scene.fbx(
            filepath=output_path,
            use_selection=True,
            object_types={"ARMATURE", "EMPTY", "MESH"},
            use_mesh_modifiers=True,
            use_armature_deform_only=True,
            add_leaf_bones=False,
            bake_space_transform=False,
            bake_anim=action is not None,
            bake_anim_use_all_actions=False,
            bake_anim_use_nla_strips=False,
            bake_anim_force_startend_keying=True,
            axis_forward="-Z",
            axis_up="Y",
            global_scale=1.0,
            apply_unit_scale=True,
            path_mode="AUTO",
            embed_textures=False,
        )
    finally:
        if action is not None and active.type == "ARMATURE":
            active.animation_data.action = previous_action
        _restore_selection(context, previous_selection, previous_active)


def copy_texture(source_path, package_root, copied):
    digest = sha256_file(source_path)
    if source_path in copied:
        return copied[source_path]
    filename = f"{digest[:12]}_{safe_name(os.path.basename(source_path))}"
    package_relative = relative_path(os.path.join("textures", filename))
    destination = os.path.join(package_root, package_relative)
    os.makedirs(os.path.dirname(destination), exist_ok=True)
    shutil.copy2(source_path, destination)
    copied[source_path] = package_relative
    return package_relative


def make_file_entry(kind, package_root, package_relative):
    disk_path = os.path.join(package_root, package_relative)
    return {
        "kind": kind,
        "relativePath": relative_path(package_relative),
        "sha256": sha256_file(disk_path),
    }


def archive_blend(package_root):
    blend_path = bpy.data.filepath
    if not blend_path or not os.path.isfile(blend_path):
        raise ExoBridgeError("Salve o arquivo .blend antes de exportar; a proveniencia arquivada e obrigatoria.")
    archive_relative = relative_path(os.path.join("source", safe_name(os.path.basename(blend_path)) + ".zip"))
    archive_path = os.path.join(package_root, archive_relative)
    os.makedirs(os.path.dirname(archive_path), exist_ok=True)
    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        archive.write(blend_path, arcname=safe_name(os.path.basename(blend_path)))
    return archive_relative


def build_manifest(package_id, entity_name, category, material_slots, model_relative, animation_entries, archive_relative):
    return {
        "schemaVersion": SCHEMA_VERSION,
        "packageId": package_id,
        "exportedAtUtc": datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z"),
        "entity": {"name": entity_name, "category": category},
        "exporter": {
            "addonVersion": ADDON_VERSION,
            "blenderVersion": ".".join(str(value) for value in bpy.app.version),
            "sourceBlendFilename": safe_name(os.path.basename(bpy.data.filepath)),
        },
        "exportSettings": {
            "forwardAxis": "-Z",
            "upAxis": "Y",
            "globalScale": 1.0,
            "applyUnitScale": True,
        },
        "files": [],
        "materials": material_slots,
        "animations": animation_entries,
    }


def export_package(context, settings):
    if bpy.app.version < (5, 2, 0):
        raise ExoBridgeError("Exo Bridge v1 requer Blender 5.2 ou mais recente.")
    if settings.root_object is None:
        raise ExoBridgeError("Escolha o objeto raiz da entidade.")
    if not settings.entity_name.strip():
        raise ExoBridgeError("Informe o nome exato cadastrado no ExoToolConfig.")
    assets_path = bpy.path.abspath(settings.assets_path)
    if os.path.basename(os.path.normpath(assets_path)) != "Assets":
        raise ExoBridgeError("Assets Path precisa apontar para a pasta Assets do projeto Unity.")

    incoming_root = os.path.join(assets_path, "ExoBridge", "Incoming")
    package_id = str(uuid.uuid4())
    package_root = os.path.join(incoming_root, safe_name(settings.entity_name), package_id)
    if os.path.exists(package_root):
        raise ExoBridgeError("O UUID do pacote ja existe; tente exportar novamente.")

    objects = collect_export_objects(settings.root_object)
    slots = collect_material_slots(objects)
    actions, armature = selected_actions(objects, settings.export_all_actions)
    if actions and armature is None:
        raise ExoBridgeError("Actions selecionadas exigem uma Armature dentro da raiz exportada.")

    os.makedirs(package_root, exist_ok=False)
    try:
        model_relative = relative_path(os.path.join("model", safe_name(settings.entity_name) + ".fbx"))
        model_path = os.path.join(package_root, model_relative)
        os.makedirs(os.path.dirname(model_path), exist_ok=True)
        export_fbx(context, objects, settings.root_object, model_path)

        copied = {}
        manifest_materials = []
        for slot in slots:
            manifest_materials.append({
                "slotName": slot["slotName"],
                "baseTexturePath": copy_texture(slot["baseSourcePath"], package_root, copied),
                "shadingTexturePath": copy_texture(slot["shadingSourcePath"], package_root, copied) if slot["shadingSourcePath"] else "",
            })

        animation_entries = []
        for action in actions:
            animation_relative = relative_path(os.path.join("animations", safe_name(action.name) + ".fbx"))
            animation_path = os.path.join(package_root, animation_relative)
            os.makedirs(os.path.dirname(animation_path), exist_ok=True)
            export_fbx(context, objects, armature, animation_path, action)
            animation_entries.append({"actionName": action.name, "filePath": animation_relative})

        archive_relative = archive_blend(package_root)
        manifest = build_manifest(
            package_id, settings.entity_name.strip(), settings.category, manifest_materials,
            model_relative, animation_entries, archive_relative,
        )
        manifest["files"].append(make_file_entry("model", package_root, model_relative))
        for texture_relative in sorted(set(copied.values())):
            manifest["files"].append(make_file_entry("texture", package_root, texture_relative))
        for animation in animation_entries:
            manifest["files"].append(make_file_entry("animation", package_root, animation["filePath"]))
        manifest["files"].append(make_file_entry("source_blend_archive", package_root, archive_relative))

        with open(os.path.join(package_root, "exo-package.json"), "w", encoding="utf-8", newline="\n") as output:
            json.dump(manifest, output, indent=2, ensure_ascii=False)
            output.write("\n")
        return package_root
    except Exception:
        # Nao deixa um pacote parcialmente gravado parecer uma evidencia valida.
        shutil.rmtree(package_root, ignore_errors=True)
        raise


class EXO_BRIDGE_PG_settings(PropertyGroup):
    assets_path: StringProperty(
        name="Assets Path",
        description="Pasta Assets do projeto ExoBeast Unity",
        subtype="DIR_PATH",
    )
    entity_name: StringProperty(
        name="Entidade",
        description="Nome exato ja cadastrado no ExoToolConfig",
    )
    category: EnumProperty(
        name="Categoria",
        items=[
            ("Personagens", "Personagens", "Personagens jogaveis e suas torres"),
            ("Monstros", "Monstros", "Inimigos"),
            ("Environment", "Environment", "Cenarios e edificios"),
        ],
        default="Personagens",
    )
    root_object: PointerProperty(
        name="Raiz",
        description="Objeto raiz e seus descendentes a exportar",
        type=bpy.types.Object,
    )
    export_all_actions: BoolProperty(
        name="Exportar todas as Actions",
        description="Exporta cada Action com curvas como FBX separado. Desative para exportar somente a Action ativa da Armature.",
        default=True,
    )


class EXO_BRIDGE_OT_export(Operator):
    bl_idname = "exo_bridge.export_package"
    bl_label = "Exportar pacote Exo Bridge"
    bl_description = "Cria pacote auditavel em Assets/ExoBridge/Incoming"

    def execute(self, context):
        try:
            path = export_package(context, context.scene.exo_bridge_settings)
        except ExoBridgeError as error:
            self.report({"ERROR"}, str(error))
            return {"CANCELLED"}
        except Exception as error:
            self.report({"ERROR"}, "Falha inesperada do Exo Bridge: " + str(error))
            return {"CANCELLED"}
        self.report({"INFO"}, "Pacote Exo Bridge criado: " + path)
        return {"FINISHED"}


class EXO_BRIDGE_PT_export(Panel):
    bl_label = "Exo Bridge Export"
    bl_idname = "EXO_BRIDGE_PT_export"
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_category = "Exo Bridge"

    def draw(self, context):
        layout = self.layout
        settings = context.scene.exo_bridge_settings
        layout.prop(settings, "assets_path")
        layout.prop(settings, "entity_name")
        layout.prop(settings, "category")
        layout.prop(settings, "root_object")
        layout.prop(settings, "export_all_actions")
        layout.separator()
        layout.label(text="Escala 1 | Forward -Z | Up Y", icon="INFO")
        layout.label(text="Apenas imagens e Actions; sem nodes Blender", icon="ERROR")
        layout.operator(EXO_BRIDGE_OT_export.bl_idname, icon="EXPORT")


_CLASSES = (
    EXO_BRIDGE_PG_settings,
    EXO_BRIDGE_OT_export,
    EXO_BRIDGE_PT_export,
)


def register():
    for cls in _CLASSES:
        bpy.utils.register_class(cls)
    bpy.types.Scene.exo_bridge_settings = PointerProperty(type=EXO_BRIDGE_PG_settings)


def unregister():
    del bpy.types.Scene.exo_bridge_settings
    for cls in reversed(_CLASSES):
        bpy.utils.unregister_class(cls)


if __name__ == "__main__":
    register()
