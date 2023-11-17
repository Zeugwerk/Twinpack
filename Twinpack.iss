; Script generated by the Inno Script Studio Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

 ;#define MyAppName "Twinpack"
 ;#define MyAppVersion "0.1.0.0"
 ;#define MyConfiguration "Debug"

#define MyAppPublisher "Zeugwerk GmbH"
#define MyAppURL "http://www.zeugwerk.at/"
#define MyAppExeName "Twinpack.exe"
#define TcXaeShellExtensionsFolder15 "C:\Program Files (x86)\Beckhoff\TcXaeShell\Common7\IDE\Extensions\"
#define TcXaeShellExtensionsFolder17 "C:\Program Files\Beckhoff\TcXaeShell\Common7\IDE\Extensions\"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{326905EF-5BAA-50D5-9F26-B205B58C91EF}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
CreateAppDir=no
OutputBaseFilename={#MyAppName} {#MyAppVersion}
Compression=lzma
SolidCompression=yes
VersionInfoCompany=Zeugwerk GmbH
VersionInfoProductName=Zeugwerk Creator
CloseApplications=force
RestartApplications=True
SetupIconFile=Zeugwerk.ico
WizardSmallImageFile=Zeugwerk.bmp
LicenseFile=LICENSE
InfoBeforeFile=DISCLAIMER

[Files]
Source: "TwinpackVsix.15\bin\{#MyConfiguration}\Package\*"; DestDir: "{#TcXaeShellExtensionsFolder15}Zeugwerk\Twinpack"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallVsixInTcXaeShell15;
Source: "TwinpackVsix.17\bin\{#MyConfiguration}\Package\*"; DestDir: "{#TcXaeShellExtensionsFolder17}Zeugwerk\Twinpack"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallVsixInTcXaeShell17;
Source: "TwinpackCli\bin\{#MyConfiguration}\*"; DestDir: "C:\Program Files (x86)\{#MyAppPublisher}\Twinpack"; Flags: ignoreversion recursesubdirs createallsubdirs; Check: InstallCliInProgramFiles;
Source: "TwinpackVsix.15\bin\{#MyConfiguration}\TwinpackVsix.15.vsix"; DestDir: "{tmp}"; Flags: deleteafterinstall;
Source: "TwinpackVsix.17\bin\{#MyConfiguration}\TwinpackVsix.17.vsix"; DestDir: "{tmp}"; Flags: deleteafterinstall;
Source: "vswhere.exe"; DestDir: "C:\Program Files (x86)\{#MyAppPublisher}\Utils"; Flags: ignoreversion;

[Dirs]
Name: "C:\Program Files (x86)\Beckhoff\TcXaeShell\Common7\IDE\Extensions\Zeugwerk\Twinpack"
Name: "C:\Program Files\Beckhoff\TcXaeShell\Common7\IDE\Extensions\Zeugwerk\Twinpack"
Name: "C:\Program Files (x86)\{#MyAppPublisher}\Twinpack"
Name: "C:\Program Files (x86)\{#MyAppPublisher}\Utils"

[InstallDelete]
Type: filesandordirs; Name: "{#TcXaeShellExtensionsFolder15}Zeugwerk\Twinpack\*"
Type: filesandordirs; Name: "{#TcXaeShellExtensionsFolder17}Zeugwerk\Twinpack\*"

[Code]
function VsWhereValue(ParameterName: string; OutputData: string): TStringList;
var
  Lines: TStringList;
  Line: string;
  i: integer;
  begin
    Result := TStringList.Create;
    Lines := TStringList.Create;
    try
      Lines.Text := OutputData;
      for i := 0 TO Lines.Count - 1 do
      begin
        Line := Lines[i];
        if Pos(ParameterName + ':', Line) > 0 then
        begin
          Result.Add(Trim(Copy(Line, Pos(ParameterName + ':', Line) + Length(ParameterName) + 2, MaxInt)));
        end;
      end;
    finally
      Lines.Free;
  end;
end;

procedure OpenPrivacyPolicy(Sender : TObject);
var
  ErrorCode : Integer;
begin
  ShellExec('open', 'https://github.com/Zeugwerk/Twinpack/blob/main/PRIVACY_POLICY.md', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

procedure OpenDiscussionPage(Sender : TObject);
var
  ErrorCode : Integer;
begin
  ShellExec('open', 'https://github.com/Zeugwerk/Twinpack/discussions', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
end;

// Exec with output stored in result.
// ResultString will only be altered if True is returned.
function ExecWithResult(Filename, Params, WorkingDir: String; ShowCmd: Integer; Wait: TExecWait; var ResultCode: Integer; var ResultString: String): Boolean;
var
  TempFilename: String;
  Command: String;
  ResultStringAnsi: AnsiString;
begin
  TempFilename := ExpandConstant('{tmp}\~execwithresult.txt');
  Command := Format('"%s" /S /C ""%s" %s > "%s""', [ExpandConstant('{cmd}'), Filename, Params, TempFilename]);
  Result := Exec(ExpandConstant('{cmd}'), Command, WorkingDir, ShowCmd, Wait, ResultCode);
  if not Result then
    Exit;
  LoadStringFromFile(TempFilename, ResultStringAnsi); 
  ResultString := ResultStringAnsi;
  DeleteFile(TempFilename);
  // Remove new-line at the end
  if (Length(ResultString) >= 2) and (ResultString[Length(ResultString) - 1] = #13) and (ResultString[Length(ResultString)] = #10) then
    Delete(ResultString, Length(ResultString) - 1, 2);
end;

var
  UserPage : TInputQueryWizardPage;
  UserPagePolicyLabel : TLabel;
  VisualStudioOptionsPage: TInputOptionWizardPage;
  UserPageRegisterButton: TNewButton;

  OutputFile: string;
  OutputData: AnsiString;
  InstallationPaths15: TStringList;
  InstallationPaths17: TStringList;  
  DisplayNames15: TStringList;
  DisplayNames17: TStringList;  
  ErrorCode: integer;
  i : integer; 
  TwinpackVsixGuid15 : string;
  TwinpackVsixGuid17 : string;  
  VsWhereOutput15 : string;
  VsWhereOutput17 : string;
  UninstallFirstPage: TNewNotebookPage;
  UninstallButton: TNewButton;

function ValidateEmail(strEmail : String) : boolean;
var
    strTemp  : String;
    nSpace   : Integer;
    nAt      : Integer;
    nDot     : Integer;
begin
    strEmail := Trim(strEmail);
    nSpace := Pos(' ', strEmail);
    nAt := Pos('@', strEmail);
    strTemp := Copy(strEmail, nAt + 1, Length(strEmail) - nAt + 1);
    nDot := Pos('.', strTemp) + nAt;
    Result := ((nSpace = 0) and (1 < nAt) and (nAt + 1 < nDot) and (nDot < Length(strEmail)));
end;

procedure RegisterEnable(Sender: TObject);
begin
   UserPageRegisterButton.Enabled := ValidateEmail(UserPage.Edits[0].Text);
end;

function IntToHex(Value: Integer): string;
begin
  Result := Format('%.2x', [Value]);
end;

function UrlEncode(data: AnsiString): AnsiString;
var
  i : Integer;
begin
  Result := '';
  for i := 1 to Length(data) do begin
    if ((Ord(data[i]) < 65) or (Ord(data[i]) > 90)) and ((Ord(data[i]) < 97) or (Ord(data[i]) > 122)) then begin
      Result := Result + '%' + IntToHex(Ord(data[i]));
    end else
      Result := Result + data[i];
  end;
end;

procedure Register(Sender: TObject);
var
  WinHttpReq: Variant;
begin
  WinHttpReq := CreateOleObject('WinHttp.WinHttpRequest.5.1');
  WinHttpReq.Open('GET', 'https://operations.zeugwerk.dev/api.php?method=zkregister&usermail='+UrlEncode(UserPage.Edits[0].Text), False);
  WinHttpReq.Send('');
  if WinHttpReq.Status <> 200 then
  begin
      MsgBox('Could not connect to Login server. Please check your internet connection!', mbError, MB_OK);
  end
    else
  begin
    if Pos('HTTP/1.1 200', Trim(WinHttpReq.ResponseText)) > 0 then
    begin
      WizardForm.NextButton.OnClick(nil);
    end
      else
    begin
      MsgBox('This email adress is already reserved or used. You might already be registered with this email address.', mbError, MB_OK);
    end;
  end;
end;

procedure InitializeWizard;
begin
  TwinpackVsixGuid15 := 'TwinpackVsix15.26e0356d-ac0e-4e6a-a50d-dd2a812f6f23';
  TwinpackVsixGuid17 := 'TwinpackVsix17.26e0356d-ac0e-4e6a-a50d-dd2a812f6f23';

  ExtractTemporaryFile('vswhere.exe');
  ExecWithResult(ExpandConstant('{tmp}\\vswhere.exe'), '-all -products * -requiresAny -requires Microsoft.VisualStudio.Product.Community Microsoft.VisualStudio.Product.Professional Microsoft.VisualStudio.Product.Enterprise -version [15.0,17.0)', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode, VsWhereOutput15);
  ExecWithResult(ExpandConstant('{tmp}\\vswhere.exe'), '-all -products * -requiresAny -requires Microsoft.VisualStudio.Product.Community Microsoft.VisualStudio.Product.Professional Microsoft.VisualStudio.Product.Enterprise -version [17.0,18.0)', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode, VsWhereOutput17);
  
  { Create the pages }
  
  { UserPage }
  UserPage := CreateInputQueryPage(wpWelcome,
    'Welcome to the Twinpack Installer', 'Twinpack is a package manage tool to faciliate sharing of TwinCAT libraries within the community.',
    'Register a Twinpack account (only needed if you want to publish packages to the Twinpack Server)');
  UserPage.Add('Email:', False);
  UserPage.Edits[0].OnChange := @RegisterEnable;

  UserPageRegisterButton := TNewButton.Create(UserPage);
  UserPageRegisterButton.Left := UserPage.Edits[0].Left;
  UserPageRegisterButton.Top := UserPage.Edits[0].Top + UserPage.Edits[0].Height + ScaleY(5);
  UserPageRegisterButton.Width := WizardForm.NextButton.Width;   
  UserPageRegisterButton.Height := WizardForm.NextButton.Height;
  UserPageRegisterButton.Parent := UserPage.Edits[0].Parent;
  UserPageRegisterButton.ParentFont := True;
  UserPageRegisterButton.Caption := 'Register';
  UserPageRegisterButton.OnClick  := @Register;  
  UserPageRegisterButton.Enabled := False;   

  UserPagePolicyLabel := TLabel.Create(UserPage);
  UserPagePolicyLabel.Left := UserPageRegisterButton.Left + UserPageRegisterButton.Width + ScaleX(5);
  UserPagePolicyLabel.Top := UserPageRegisterButton.Top + ScaleY(5);
  UserPagePolicyLabel.Parent := UserPage.Edits[0].Parent;
  UserPagePolicyLabel.Caption := 'Privacy policy';
  UserPagePolicyLabel.OnClick := @OpenPrivacyPolicy;
  UserPagePolicyLabel.Font.Style := UserPagePolicyLabel.Font.Style + [fsUnderline];
  UserPagePolicyLabel.Font.Color := clBlue;
  UserPagePolicyLabel.Cursor := crHand; 

  { VisualStudioOptionsPage } 
	VisualStudioOptionsPage := CreateInputOptionPage(UserPage.ID,
	  'Install options', 'Twinpack is compatible with multiple IDEs',
	  'Please choose the Visual Studio versions that Twinpack is installed for.',
	  False, False);

	// Add items
  VisualStudioOptionsPage.Add('TcXAEShell (32-bit)');
  if(FileExists('C:\Program Files (x86)\Beckhoff\TcXaeShell\Common7\IDE\TcXaeShell.exe')) then
    VisualStudioOptionsPage.CheckListBox.Checked[0] := true
  else
    VisualStudioOptionsPage.CheckListBox.ItemEnabled[0] := false;

  VisualStudioOptionsPage.Add('TcXAEShell (64-bit)');
  if(FileExists('C:\Program Files\Beckhoff\TcXaeShell\Common7\IDE\TcXaeShell.exe')) then
    VisualStudioOptionsPage.CheckListBox.Checked[1] := true
  else
    VisualStudioOptionsPage.CheckListBox.ItemEnabled[1] := false;
	
  VisualStudioOptionsPage.Add('Twinpack Cli');
  VisualStudioOptionsPage.CheckListBox.Checked[2] := true
  VisualStudioOptionsPage.CheckListBox.ItemEnabled[2] := true;

  DisplayNames15 := VsWhereValue('displayName', VsWhereOutput15);
  InstallationPaths15 := VsWhereValue('installationPath', VsWhereOutput15); 

  for i:= 0 to DisplayNames15.Count-1 do
  begin
    if(FileExists(InstallationPaths15[i] + '\Common7\IDE\VSIXInstaller.exe')) then
      VisualStudioOptionsPage.Add(DisplayNames15[i]);
  end;
  
  DisplayNames17 := VsWhereValue('displayName', VsWhereOutput17);
  InstallationPaths17 := VsWhereValue('installationPath', VsWhereOutput17); 

  for i:= 0 to DisplayNames17.Count-1 do
  begin
    if(FileExists(InstallationPaths17[i] + '\Common7\IDE\VSIXInstaller.exe')) then
      VisualStudioOptionsPage.Add(DisplayNames17[i]);
  end;  
end;

function InstallVsixInTcXaeShell15(): Boolean;
begin
  Result := VisualStudioOptionsPage.CheckListBox.Checked[0] = True;
end;

function InstallCliInProgramFiles(): Boolean;
begin
  Result := VisualStudioOptionsPage.CheckListBox.Checked[2] = True;
end;

function InstallVsixInTcXaeShell17(): Boolean;
begin
  Result := VisualStudioOptionsPage.CheckListBox.Checked[1] = True;
end;

procedure CurStepChanged (CurStep: TSetupStep);
var
  WorkingDir:   String;
  ReturnCode:   Integer;
  i:            Integer;
begin  
  if (ssInstall = CurStep) then
  begin
    ExtractTemporaryFile('TwinpackVsix.15.vsix');
    ExtractTemporaryFile('TwinpackVsix.17.vsix');
	
    for i := 0 to DisplayNames15.Count-1 do
    begin
      if(VisualStudioOptionsPage.CheckListBox.Checked[i+3]) then
      begin
        ShellExec('', InstallationPaths15[i] + '\Common7\IDE\VSIXInstaller.exe', '/u:'+TwinpackVsixGuid15+' /quiet', '', SW_HIDE, ewWaitUntilTerminated, ReturnCode);
        ShellExec('', InstallationPaths15[i] + '\Common7\IDE\VSIXInstaller.exe', '/force ' + ExpandConstant('{tmp}\TwinpackVsix.15.vsix'), '', SW_HIDE, ewWaitUntilTerminated, ReturnCode);
      end;
    end;
	
    for i := 0 to DisplayNames17.Count-1 do
    begin
      if(VisualStudioOptionsPage.CheckListBox.Checked[i+3+DisplayNames15.Count]) then
      begin
        ShellExec('', InstallationPaths17[i] + '\Common7\IDE\VSIXInstaller.exe', '/u:'+TwinpackVsixGuid17+' /quiet', '', SW_HIDE, ewWaitUntilTerminated, ReturnCode);
        ShellExec('', InstallationPaths17[i] + '\Common7\IDE\VSIXInstaller.exe', '/force ' + ExpandConstant('{tmp}\TwinpackVsix.17.vsix'), '', SW_HIDE, ewWaitUntilTerminated, ReturnCode);
      end;
    end;	
  end;
end;

function InitializeUninstall(): Boolean;
begin
  ExecWithResult(ExpandConstant('C:\Program Files (x86)\\{#MyAppPublisher}\\Utils\\vswhere.exe'), '-all -products * -requiresAny -requires Microsoft.VisualStudio.Product.Community Microsoft.VisualStudio.Product.Professional Microsoft.VisualStudio.Product.Enterprise -version [15.0,17.0)', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode, VsWhereOutput15);
  ExecWithResult(ExpandConstant('C:\Program Files (x86)\\{#MyAppPublisher}\\Utils\\vswhere.exe'), '-all -products * -requiresAny -requires Microsoft.VisualStudio.Product.Community Microsoft.VisualStudio.Product.Professional Microsoft.VisualStudio.Product.Enterprise -version [17.0,18.0)', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode, VsWhereOutput17);

  DisplayNames15 := VsWhereValue('displayName', VsWhereOutput15);
  InstallationPaths15 := VsWhereValue('installationPath', VsWhereOutput15);   
  DisplayNames17 := VsWhereValue('displayName', VsWhereOutput17);
  InstallationPaths17 := VsWhereValue('installationPath', VsWhereOutput17);  
  
  Result := True;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ReturnCode : Integer;
begin
  TwinpackVsixGuid15 := 'TwinpackVsix15.26e0356d-ac0e-4e6a-a50d-dd2a812f6f23';
  TwinpackVsixGuid17 := 'TwinpackVsix17.26e0356d-ac0e-4e6a-a50d-dd2a812f6f23';

  case CurUninstallStep of
    usUninstall:
      begin
        for i:= 0 to DisplayNames15.Count-1 do
        begin
          ShellExec('', InstallationPaths15[i] + '\Common7\IDE\VSIXInstaller.exe', '/u:'+TwinpackVsixGuid15+' /quiet', '', SW_HIDE, ewWaitUntilTerminated, ReturnCode);  
        end;        

        for i:= 0 to DisplayNames17.Count-1 do
        begin
          ShellExec('', InstallationPaths17[i] + '\Common7\IDE\VSIXInstaller.exe', '/u:'+TwinpackVsixGuid17+' /quiet', '', SW_HIDE, ewWaitUntilTerminated, ReturnCode);  
        end;        
      end;	  
    usPostUninstall:
      begin
        // ...insert code to perform post-uninstall tasks here...
      end;
  end;
end;

// --------------------------------------------------------------------------------------------------------------------------
// Uninstall Behavior
// --------------------------------------------------------------------------------------------------------------------------
procedure UpdateUninstallWizard;
begin
  UninstallButton.Caption := 'Uninstall';
  // Make the "Uninstall" button break the ShowModal loop
  UninstallButton.ModalResult := mrOK;
end;  

procedure UninstallButtonClick(Sender: TObject);
begin
    UninstallButton.Visible := False;
    UpdateUninstallWizard;
end;

procedure InitializeUninstallProgressForm();
var
  UninstallText: TNewStaticText;
  UserPageOpenDiscussionText: TNewStaticText;
  PageNameLabel: string;
  PageDescriptionLabel: string;
  CancelButtonEnabled: Boolean;
  CancelButtonModalResult: Integer;
begin
  if not UninstallSilent then
  begin
    // Create the first page and make it active
    UninstallFirstPage := TNewNotebookPage.Create(UninstallProgressForm);
    UninstallFirstPage.Notebook := UninstallProgressForm.InnerNotebook;
    UninstallFirstPage.Parent := UninstallProgressForm.InnerNotebook;
    UninstallFirstPage.Align := alClient;
  
    UninstallText := TNewStaticText.Create(UninstallProgressForm);
    UninstallText.Parent := UninstallFirstPage;
    UninstallText.Top := UninstallProgressForm.StatusLabel.Top;
    UninstallText.Left := UninstallProgressForm.StatusLabel.Left;
    UninstallText.Width := UninstallProgressForm.StatusLabel.Width;
    UninstallText.Height := 300; //UninstallProgressForm.StatusLabel.Height;
    UninstallText.AutoSize := False;
    UninstallText.ShowAccelChar := False;
    UninstallText.Caption := 'It was nice having you here!' #13 #10 
                        'Thanks for using Twinpack, please leave us some Feedback on:';

    UserPageOpenDiscussionText := TNewStaticText.Create(UninstallProgressForm);
    UserPageOpenDiscussionText.Parent := UninstallFirstPage;
    UserPageOpenDiscussionText.Top := UninstallProgressForm.StatusLabel.Top + ScaleY(50);
    UserPageOpenDiscussionText.Left := UninstallProgressForm.StatusLabel.Left;
    UserPageOpenDiscussionText.Width := UninstallProgressForm.StatusLabel.Width;
    UserPageOpenDiscussionText.Height := 300; //UninstallProgressForm.StatusLabel.Height;
    UserPageOpenDiscussionText.AutoSize := FALSE;
    UserPageOpenDiscussionText.Caption := 'https://github.com/Zeugwerk/Twinpack/discussions';
    UserPageOpenDiscussionText.OnClick := @OpenDiscussionPage;
    UserPageOpenDiscussionText.Font.Style := UserPageOpenDiscussionText.Font.Style + [fsUnderline];
    UserPageOpenDiscussionText.Font.Color := clBlue;
    UserPageOpenDiscussionText.Cursor := crHand; 
    
    UninstallProgressForm.InnerNotebook.ActivePage := UninstallFirstPage;

    PageNameLabel := UninstallProgressForm.PageNameLabel.Caption;
    PageDescriptionLabel := UninstallProgressForm.PageDescriptionLabel.Caption;
  
    UninstallButton := TNewButton.Create(UninstallProgressForm);
    UninstallButton.Parent := UninstallProgressForm;
    UninstallButton.Left := UninstallProgressForm.CancelButton.Left - UninstallProgressForm.CancelButton.Width - ScaleX(10);
    UninstallButton.Top := UninstallProgressForm.CancelButton.Top;
    UninstallButton.Width := UninstallProgressForm.CancelButton.Width;
    UninstallButton.Height := UninstallProgressForm.CancelButton.Height;
    UninstallButton.OnClick := @UninstallButtonClick;
    UninstallButton.TabOrder := UninstallButton.TabOrder + 1;

    UninstallProgressForm.CancelButton.TabOrder := UninstallButton.TabOrder + 1;

    // Run our wizard pages 
    UpdateUninstallWizard;
    CancelButtonEnabled := UninstallProgressForm.CancelButton.Enabled
    UninstallProgressForm.CancelButton.Enabled := True;
    CancelButtonModalResult := UninstallProgressForm.CancelButton.ModalResult;
    UninstallProgressForm.CancelButton.ModalResult := mrCancel;

    if UninstallProgressForm.ShowModal = mrCancel then Abort;

    // Restore the standard page payout
    UninstallProgressForm.CancelButton.Enabled := CancelButtonEnabled;
    UninstallProgressForm.CancelButton.ModalResult := CancelButtonModalResult;

    UninstallProgressForm.PageNameLabel.Caption := PageNameLabel;
    UninstallProgressForm.PageDescriptionLabel.Caption := PageDescriptionLabel;

    UninstallProgressForm.InnerNotebook.ActivePage := UninstallProgressForm.InstallingPage;
  end;
end;
