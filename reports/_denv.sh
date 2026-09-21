#!/bin/bash
# Escape hatch for a stripped sandbox environment: dotnet/NuGet needs these Windows
# vars non-empty or it dies with "Failed to load NuGet settings. Value cannot be null.
# (Parameter 'path1')" from NuGetEnvironment.CalculateFolderPath.
exec env \
  HOME='C:\Users\Mechrev' \
  USERPROFILE='C:\Users\Mechrev' \
  APPDATA='C:\Users\Mechrev\AppData\Roaming' \
  LOCALAPPDATA='C:\Users\Mechrev\AppData\Local' \
  PROGRAMFILES='C:\Program Files' \
  'ProgramW6432=C:\Program Files' \
  'PROGRAMFILES(X86)=C:\Program Files (x86)' \
  PROGRAMDATA='C:\ProgramData' \
  ALLUSERSPROFILE='C:\ProgramData' \
  MSYS2_ENV_CONV_EXCL='*' \
  MSYS_NO_PATHCONV=1 \
  "$@"
