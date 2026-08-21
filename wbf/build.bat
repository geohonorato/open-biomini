@echo off
call "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat" x64
cd /d "c:\Users\Geovanni\Documents\Hermes\open-biomini\wbf"
cl /LD /O2 /EHsc /I"C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\um" /I"C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\shared" BioMiniSensorAdapter.cpp /link /DEF:BioMiniSensorAdapter.def /OUT:BioMiniSensorAdapter.dll
