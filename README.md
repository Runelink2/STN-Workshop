
# STN SDK 

See the releases page for direct download.

# STN Workshop (git clone)

Survive The Nights --- Modding & Workshop SDK

This repository contains the full Unity project used to build and run
the Survive The Nights Workshop system.

## Getting Started

### Option 1 --- Git + LFS

``` bash
git lfs install
git clone https://github.com/Runelink2/STN-Workshop.git
cd STN-Workshop
git lfs pull
```

### Option 2 --- GitHub ZIP

Download ZIP then replace: Assets/\_Internal/Json/public_metadata.json

Download real file:
https://drive.google.com/file/d/1PghrVLUBcsxOXO3cnwES-IpiP_zuCZnb/view

---

## Creating Mods

### Project Structure

| Folder | Purpose | Can Modify? |
|--------|---------|-------------|
| `Assets/My_Mods/` | **Your mods go here** | Yes |
| `Assets/_Internal/` | SDK internals | **No** |

### Where to Put Your Code

Each mod has its own folder under `Assets/My_Mods/YourModName/`:

```
Assets/My_Mods/YourModName/
├── Code/                  ← Your C# scripts go here
│   └── MyModLogic.cs
├── Assets/                ← Your prefabs, audio, textures
└── modinfo.json           ← Mod metadata
```

### SDK Helpers (Read-Only)

The SDK provides helper classes you can **use** but should **not modify**:

- **`STN.ModSDK.ModContentLoader`** - Load/instantiate Addressable assets
- **`STN.ModSDK.HarmonyTargets`** - Reflection helpers for Harmony patching

These are compiled into `STN.ModSDK.dll`. Your mod references this DLL at compile time,
but at runtime the **game's copy** of the DLL is used. Modifying the source files in
`Assets/_Internal/` will have no effect on your mod.

**If you need custom helpers**, create them in your mod's `Code/` folder.

### Example: Using ModContentLoader

```csharp
using STN.ModSDK;
using UnityEngine;

public class MyMod {
    void SpawnSomething() {
        ModContentLoader.InstantiateAsync("Mod/MyMod/Assets/MyPrefab", 
            Vector3.zero, Quaternion.identity, 
            go => Debug.Log($"Spawned: {go?.name}"));
    }
}
```

### Building Your Mod

1. Open the Mod SDK window: **Window → Mod SDK**
2. Select your mod from the dropdown
3. Configure assets and addresses
4. Click **Build Mod**

The output folder will contain your mod ready for distribution.
