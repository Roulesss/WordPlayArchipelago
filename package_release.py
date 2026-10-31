#!/usr/bin/env python3
"""
Word Play Archipelago - Packaging & Release Script
Builds the Archipelago world package (word_play.apworld) and packages
the complete BepInEx + Mod game bundle (WordPlayArchipelago_v1.0.0_with_BepInEx.zip).

Usage:
    python package_release.py              # Build both apworld and mod zip
    python package_release.py --apworld    # Build only word_play.apworld
    python package_release.py --mod        # Build only BepInEx mod zip
"""

import sys
import os
import zipfile
import shutil
import subprocess
import urllib.request

BEPINEX_URL = "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip"
BEPINEX_ZIP_NAME = "BepInEx_win_x64_5.4.23.2.zip"

ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
PARENT_DIR = os.path.dirname(ROOT_DIR)
RELEASE_DIR = os.path.join(ROOT_DIR, 'Release')
EXTERNAL_RELEASE_DIR = os.path.join(PARENT_DIR, 'Release') if os.path.isdir(os.path.join(PARENT_DIR, 'Release')) else None

def build_apworld():
    print("=== Building word_play.apworld ===")
    src_world_dir = os.path.join(ROOT_DIR, 'ArchipelagoWorld')
    if not os.path.isdir(src_world_dir):
        print(f"Error: ArchipelagoWorld directory not found at {src_world_dir}")
        sys.exit(1)

    os.makedirs(RELEASE_DIR, exist_ok=True)
    dest_apworld = os.path.join(RELEASE_DIR, 'word_play.apworld')

    with zipfile.ZipFile(dest_apworld, 'w', compression=zipfile.ZIP_DEFLATED) as z:
        for root, dirs, files in os.walk(src_world_dir):
            if '__pycache__' in root:
                continue
            for file in files:
                if file.endswith('.pyc') or file.endswith('.pyo'):
                    continue
                abs_path = os.path.join(root, file)
                rel_path = os.path.relpath(abs_path, src_world_dir)
                arcname = os.path.join('word_play', rel_path).replace('\\', '/')
                z.write(abs_path, arcname)

    print(f"[OK] Built: {dest_apworld} ({os.path.getsize(dest_apworld):,} bytes)")

    if EXTERNAL_RELEASE_DIR and os.path.isdir(EXTERNAL_RELEASE_DIR):
        ext_dest = os.path.join(EXTERNAL_RELEASE_DIR, 'word_play.apworld')
        shutil.copy2(dest_apworld, ext_dest)
        print(f"[OK] Synced to external Release: {ext_dest}")

def ensure_mod_built():
    bin_release = os.path.join(ROOT_DIR, 'ArchipelagoMod', 'bin', 'Release', 'netstandard2.1')
    mod_dll = os.path.join(bin_release, 'WordPlayArchipelago.dll')
    if not os.path.isfile(mod_dll):
        print("Mod assembly not found. Compiling via dotnet build...")
        csproj_path = os.path.join(ROOT_DIR, 'ArchipelagoMod', 'ArchipelagoMod.csproj')
        cmd = ["dotnet", "build", csproj_path, "-c", "Release"]
        res = subprocess.run(cmd, cwd=ROOT_DIR)
        if res.returncode != 0:
            print("Error: dotnet build failed. Please build ArchipelagoMod before packaging.")
            sys.exit(1)
    return bin_release

def find_or_download_bepinex():
    search_dirs = [ROOT_DIR, PARENT_DIR, os.path.join(ROOT_DIR, 'downloads')]
    for d in search_dirs:
        if os.path.isdir(d):
            for f in os.listdir(d):
                if f.lower().startswith('bepinex') and f.lower().endswith('.zip'):
                    found = os.path.join(d, f)
                    print(f"Using local BepInEx archive: {found}")
                    return found

    download_dir = os.path.join(ROOT_DIR, 'downloads')
    os.makedirs(download_dir, exist_ok=True)
    dest_path = os.path.join(download_dir, BEPINEX_ZIP_NAME)
    print(f"BepInEx archive not found locally. Downloading from {BEPINEX_URL}...")
    try:
        urllib.request.urlretrieve(BEPINEX_URL, dest_path)
        print(f"[OK] Downloaded BepInEx to {dest_path}")
        return dest_path
    except Exception as ex:
        print(f"Warning: Failed to download BepInEx ({ex}). Creating mod-only package.")
        return None

def build_mod_zip():
    print("=== Building WordPlayArchipelago_v1.0.0_with_BepInEx.zip ===")
    bin_release = ensure_mod_built()
    assets_dir = os.path.join(ROOT_DIR, 'ArchipelagoMod', 'Assets')
    os.makedirs(RELEASE_DIR, exist_ok=True)
    release_zip = os.path.join(RELEASE_DIR, 'WordPlayArchipelago_v1.0.0_with_BepInEx.zip')

    bepinex_zip = find_or_download_bepinex()

    staging_dir = os.path.join(ROOT_DIR, 'staging_release')
    if os.path.exists(staging_dir):
        shutil.rmtree(staging_dir)
    os.makedirs(staging_dir, exist_ok=True)

    try:
        if bepinex_zip and os.path.isfile(bepinex_zip):
            with zipfile.ZipFile(bepinex_zip, 'r') as z:
                z.extractall(staging_dir)

        plugin_dir = os.path.join(staging_dir, 'BepInEx', 'plugins', 'WordPlayArchipelago')
        os.makedirs(plugin_dir, exist_ok=True)

        files_to_copy = [
            'WordPlayArchipelago.dll',
            'WordPlayArchipelago.pdb',
            'Archipelago.MultiClient.Net.dll',
            'Newtonsoft.Json.dll'
        ]
        for f in files_to_copy:
            src = os.path.join(bin_release, f)
            if os.path.isfile(src):
                shutil.copy2(src, plugin_dir)

        dest_assets = os.path.join(plugin_dir, 'Assets')
        if os.path.isdir(assets_dir):
            shutil.copytree(assets_dir, dest_assets, dirs_exist_ok=True)

        if os.path.exists(release_zip):
            os.remove(release_zip)

        with zipfile.ZipFile(release_zip, 'w', compression=zipfile.ZIP_DEFLATED) as z:
            for root, dirs, files in os.walk(staging_dir):
                for f in files:
                    full_path = os.path.join(root, f)
                    rel_path = os.path.relpath(full_path, staging_dir)
                    z.write(full_path, rel_path)

        print(f"[OK] Built: {release_zip} ({os.path.getsize(release_zip):,} bytes)")

        if EXTERNAL_RELEASE_DIR and os.path.isdir(EXTERNAL_RELEASE_DIR):
            ext_dest = os.path.join(EXTERNAL_RELEASE_DIR, 'WordPlayArchipelago_v1.0.0_with_BepInEx.zip')
            shutil.copy2(release_zip, ext_dest)
            print(f"[OK] Synced to external Release: {ext_dest}")

    finally:
        if os.path.exists(staging_dir):
            shutil.rmtree(staging_dir)

def main():
    args = [a.lower() for a in sys.argv[1:]]
    build_ap = True
    build_mod = True

    if '--apworld' in args or 'apworld' in args:
        build_mod = False
    elif '--mod' in args or 'mod' in args:
        build_ap = False

    if build_ap:
        build_apworld()
    if build_mod:
        build_mod_zip()

    print("\n[OK] Packaging complete!")

if __name__ == '__main__':
    main()
