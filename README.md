# RecDive1

LIMITATIONS:
- EXCEPTION CHECKS ARE MOSTLY ABSENT
- ~~NO INTERRUPTION UPON EXCEEDING MAXIMUM RECURSION DEPTH~~
- EXECUTED PROGRAM WILL NOT BE TERMINATED OVER EXCESSIVE TIME/MEMORY USE UNLESS IT IS ASSOCIATED WITH EXCESSIVE RECURSION

REMARKS:
- Entry point should be the static method
- ~~CodeLoad.cs:193 should contain actual path to the folder with all the libraries required for client code. The subsequent lines should explicitly connect those libraries (minimal required set: mscorlib.dll, System.dll)~~ CodeLoad:245 should enumerate all the assemblies required for the program tested
