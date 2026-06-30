#if FMOD_PRESENT
using UnityEngine;
using FMODUnity;
using System.Collections.Generic;
using System;

public class CreateFmodList : ScriptableObject
{
    public ListType type;
    public string typeName;
    public List<FMODListEntry> events;
}

[Serializable]
public class FMODListEntry
{
    public string id;
    public EventReference reference;
}

public enum ListType
{
    None,
    Sfx,
    Music,
    Other
}
#endif
