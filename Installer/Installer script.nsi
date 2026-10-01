!include "MUI.nsh"

Name "Do-Re-Mi Lyrics"
OutFile "Do-Re-Mi Lyrics Setup.exe"
Unicode True

InstallDir "$PROGRAMFILES\Do-Re-Mi Lyrics"
 
RequestExecutionLevel admin
 
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Run Do-Re-Mi Lyrics"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchApplication

!insertmacro MUI_PAGE_LICENSE "..\License"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Section
 
    SetOutPath $INSTDIR
 
    WriteUninstaller "$INSTDIR\uninstall.exe"
 
    CreateShortcut "$SMPROGRAMS\Do-Re-Mi Lyrics.lnk" "$INSTDIR\Do-Re-Mi Lyrics.exe"
    CreateShortcut "$SMPROGRAMS\Do-Re-Mi Lyrics Uninstall.lnk" "$INSTDIR\uninstall.exe"

    File /r "..\bin\Release\net10.0-windows\*"

SectionEnd
 
Section "uninstall"
 
    Delete "$INSTDIR\uninstall.exe"
    Delete "$SMPROGRAMS\Do-Re-Mi Lyrics.lnk"
    Delete "$SMPROGRAMS\Do-Re-Mi Lyrics Uninstall.lnk"
    Delete "$INSTDIR\*"
    Delete "$INSTDIR\ref\*"
    RMDir "$INSTDIR\ref"
    RMDir /r "$INSTDIR\Python"
    RMDir /r "$INSTDIR\ffmpeg"
    RMDir $INSTDIR

    SetShellVarContext current
    RMDir /r "$LOCALAPPDATA\Do-Re-Mi Lyrics"

SectionEnd

Function LaunchApplication
   SetOutPath $INSTDIR
   ShellExecAsUser::ShellExecAsUser "" "$INSTDIR\Do-Re-Mi Lyrics.exe" ""
FunctionEnd