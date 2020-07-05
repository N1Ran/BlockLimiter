:: This script creates a symlink to the game binaries to account for different installation directories on different systems.

@echo off
setglobal
set Torch=D:\GameFolder\Torch
endglobal
set path=%Torch%\DedicatedServer64
mklink /J GameBinaries "%path%"
if errorlevel 1 goto Error
echo Done!
goto End
:Error
echo An error occured creating the symlink.
goto EndFinal
:End


mklink /J TorchBinaries "%Torch%"
if errorlevel 1 goto Error
echo Done! You can now open the Torch solution without issue.
goto EndFinal
:Error2
echo An error occured creating the symlink.
:EndFinal
pause
