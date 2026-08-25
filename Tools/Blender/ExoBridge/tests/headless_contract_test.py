"""Run with: blender --background --python Tools/Blender/ExoBridge/tests/headless_contract_test.py"""

import importlib.util
import json
import os
import tempfile

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SPEC = importlib.util.spec_from_file_location("exo_bridge_test", os.path.join(ROOT, "__init__.py"))
ADDON = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(ADDON)


def require(condition, message):
    if not condition:
        raise AssertionError(message)


with tempfile.TemporaryDirectory() as directory:
    texture_path = os.path.join(directory, "base.png")
    image = bpy.data.images.new("ExoBridgeTest", width=2, height=2, alpha=True)
    image.filepath_raw = texture_path
    image.file_format = "PNG"
    image.save()

    bpy.data.images.remove(image)
    image = bpy.data.images.load(texture_path, check_existing=False)
    require(ADDON.require_supported_image(image, "teste") == texture_path, "PNG deve ser suportado")

    manifest = ADDON.build_manifest(
        "123e4567-e89b-12d3-a456-426614174000",
        "EntidadeTeste",
        "Personagens",
        [{"slotName": "Cube[0]::Mat", "baseTexturePath": "textures/base.png", "shadingTexturePath": ""}],
        "model/EntidadeTeste.fbx",
        [{"actionName": "Idle", "filePath": "animations/Idle.fbx"}],
        "source/test.blend.zip",
    )
    require(manifest["schemaVersion"] == 1, "schemaVersion incorreto")
    require(manifest["exportSettings"] == {"forwardAxis": "-Z", "upAxis": "Y", "globalScale": 1.0, "applyUnitScale": True}, "eixos/escala incorretos")
    require(manifest["animations"][0]["actionName"] == "Idle", "Action ausente")
    require(len(ADDON.sha256_file(texture_path)) == 64, "SHA-256 invalido")
    json.dumps(manifest)

print("Exo Bridge Blender contract tests: PASS")
