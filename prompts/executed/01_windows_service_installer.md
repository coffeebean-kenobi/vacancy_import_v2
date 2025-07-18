# Windows Service インストール機能実装プロンプト

## 📖 概要
VacancyImportプロジェクトにWindows Serviceインストール機能を実装する。.NET Framework Windows Serviceドキュメントに基づいて、ProjectInstaller、ServiceInstaller、ServiceProcessInstallerを作成し、installutil.exeでのインストール/アンインストールを可能にする。

## 🎯 実装対象
- ProjectInstallerクラスの作成
- ServiceInstallerの設定
- ServiceProcessInstallerの設定
- インストール/アンインストール用のバッチファイル作成

## 📋 詳細仕様

### 1. ProjectInstallerクラス作成

**ファイル**: `src/VacancyImport/ProjectInstaller.cs`

```csharp
using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace VacancyImport
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        private ServiceInstaller serviceInstaller;
        private ServiceProcessInstaller serviceProcessInstaller;

        public ProjectInstaller()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.serviceInstaller = new ServiceInstaller();
            this.serviceProcessInstaller = new ServiceProcessInstaller();

            // ServiceInstaller設定
            this.serviceInstaller.ServiceName = "VacancyImportService";
            this.serviceInstaller.DisplayName = "予約管理システム連携サービス";
            this.serviceInstaller.Description = "Excelファイルから予約データを抽出してSupabaseに同期し、LINE WORKSに通知するサービス";
            this.serviceInstaller.StartType = ServiceStartMode.Automatic;

            // ServiceProcessInstaller設定
            this.serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            this.serviceProcessInstaller.Username = null;
            this.serviceProcessInstaller.Password = null;

            // Installersコレクションに追加
            this.Installers.AddRange(new Installer[] {
                this.serviceProcessInstaller,
                this.serviceInstaller
            });
        }

        protected override void OnBeforeInstall(IDictionary savedState)
        {
            // カスタムインストール前処理
            base.OnBeforeInstall(savedState);
            
            // イベントログソースの作成
            if (!System.Diagnostics.EventLog.SourceExists("VacancyImportService"))
            {
                System.Diagnostics.EventLog.CreateEventSource("VacancyImportService", "Application");
            }
        }

        protected override void OnAfterInstall(IDictionary savedState)
        {
            base.OnAfterInstall(savedState);
            
            // インストール後の設定
            Console.WriteLine("予約管理システム連携サービスのインストールが完了しました。");
        }

        protected override void OnBeforeUninstall(IDictionary savedState)
        {
            base.OnBeforeUninstall(savedState);
            
            // アンインストール前の処理
            Console.WriteLine("予約管理システム連携サービスをアンインストールしています...");
        }

        protected override void OnAfterUninstall(IDictionary savedState)
        {
            base.OnAfterUninstall(savedState);
            
            // イベントログソースの削除
            if (System.Diagnostics.EventLog.SourceExists("VacancyImportService"))
            {
                System.Diagnostics.EventLog.DeleteEventSource("VacancyImportService");
            }
            
            Console.WriteLine("予約管理システム連携サービスのアンインストールが完了しました。");
        }
    }
}
```

### 2. プロジェクトファイル更新

**ファイル**: `src/VacancyImport/VacancyImport.csproj`

以下のパッケージ参照を追加：
```xml
<PackageReference Include="System.Configuration.ConfigurationManager" Version="8.0.0" />
<PackageReference Include="System.Configuration.Install" Version="4.7.0" />
```

### 3. インストール用バッチファイル作成

**ファイル**: `scripts/install_service.bat`

```batch
@echo off
echo 予約管理システム連携サービス インストール
echo =======================================

REM 管理者権限チェック
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo エラー: このスクリプトは管理者権限で実行してください。
    pause
    exit /b 1
)

REM サービス停止・削除（既存の場合）
sc query VacancyImportService >nul 2>&1
if %errorlevel% equ 0 (
    echo 既存のサービスを停止・削除しています...
    sc stop VacancyImportService
    timeout /t 5 /nobreak >nul
    sc delete VacancyImportService
    timeout /t 3 /nobreak >nul
)

REM ビルド
echo アプリケーションをビルドしています...
cd /d "%~dp0..\src\VacancyImport"
dotnet build --configuration Release
if %errorlevel% neq 0 (
    echo エラー: ビルドに失敗しました。
    pause
    exit /b 1
)

REM インストール
echo サービスをインストールしています...
cd bin\Release\net8.0
%windir%\Microsoft.NET\Framework64\v4.0.30319\installutil.exe VacancyImport.exe
if %errorlevel% neq 0 (
    echo エラー: サービスのインストールに失敗しました。
    pause
    exit /b 1
)

REM サービス開始
echo サービスを開始しています...
sc start VacancyImportService
if %errorlevel% neq 0 (
    echo 警告: サービスの開始に失敗しました。手動で開始してください。
)

echo.
echo インストールが完了しました！
echo サービス管理: services.msc
echo イベントログ: eventvwr.msc
echo.
pause
```

**ファイル**: `scripts/uninstall_service.bat`

```batch
@echo off
echo 予約管理システム連携サービス アンインストール
echo =========================================

REM 管理者権限チェック
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo エラー: このスクリプトは管理者権限で実行してください。
    pause
    exit /b 1
)

REM サービス停止
echo サービスを停止しています...
sc stop VacancyImportService
timeout /t 10 /nobreak >nul

REM アンインストール
echo サービスをアンインストールしています...
cd /d "%~dp0..\src\VacancyImport\bin\Release\net8.0"
%windir%\Microsoft.NET\Framework64\v4.0.30319\installutil.exe /u VacancyImport.exe
if %errorlevel% neq 0 (
    echo エラー: サービスのアンインストールに失敗しました。
    pause
    exit /b 1
)

echo.
echo アンインストールが完了しました！
echo.
pause
```

### 4. 開発者向けヘルパースクリプト

**ファイル**: `scripts/debug_service.bat`

```batch
@echo off
echo 予約管理システム連携サービス デバッグモード
echo ==========================================

echo コンソールアプリケーションとして実行します...
cd /d "%~dp0..\src\VacancyImport"

REM 開発環境設定
set ASPNETCORE_ENVIRONMENT=Development

dotnet run --configuration Debug
pause
```

## 🔍 検証手順

1. **ビルド確認**:
   ```bash
   cd src/VacancyImport
   dotnet build --configuration Release
   ```

2. **インストールテスト** (管理者コマンドプロンプトで):
   ```bash
   scripts\install_service.bat
   ```

3. **サービス状態確認**:
   ```bash
   sc query VacancyImportService
   services.msc
   ```

4. **アンインストールテスト**:
   ```bash
   scripts\uninstall_service.bat
   ```

## ⚠️ 注意事項

- 管理者権限でのみ実行可能
- .NET Framework 4.0の installutil.exe を使用
- イベントログソースの作成/削除を含む
- 既存サービスの確認と適切な停止処理

## 📚 参考ドキュメント

- [.NET Framework Windows サービス](https://learn.microsoft.com/ja-jp/dotnet/framework/windows-services/)
- [サービスのインストールとアンインストール](https://learn.microsoft.com/ja-jp/dotnet/framework/windows-services/how-to-install-and-uninstall-services)

## 🎯 完了条件

- [ ] ProjectInstallerクラスが作成されている
- [ ] インストール/アンインストール用バッチファイルが作成されている
- [ ] installutil.exeでのインストール/アンインストールが正常に動作する
- [ ] Windows サービス管理コンソールでサービスが確認できる
- [ ] イベントログソースが適切に作成/削除される 