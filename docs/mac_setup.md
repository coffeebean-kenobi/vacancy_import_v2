# Mac開発環境セットアップガイド

## 前提条件

- macOS 11.0以降
- Homebrew（パッケージマネージャー）
- .NET SDK 8.0以降（必須）

## 1. 必要なツールのインストール

### .NET SDKのインストール

**重要**: このプロジェクトは.NET 8.0を必要とします。古いバージョンの.NET SDKではビルドできません。

#### .NET 8.0のインストール方法

Homebrewを使用してインストールする場合:

```bash
# 最新の.NET SDKをインストール
brew install --cask dotnet-sdk
```

または、[.NET 8.0公式ダウンロードページ](https://dotnet.microsoft.com/download/dotnet/8.0)から直接インストーラーをダウンロードすることもできます。

インストール後、以下のコマンドでバージョンを確認できます:

```bash
dotnet --version
```

バージョンが8.0以上であることを確認してください。

#### 複数バージョンの.NET SDKがインストールされている場合

複数のバージョンの.NET SDKがインストールされている場合は、以下のコマンドで利用可能なすべてのバージョンを確認できます:

```bash
dotnet --list-sdks
```

グローバル.json ファイルを作成して、プロジェクトで使用するSDKバージョンを指定することもできます:

```bash
dotnet new globaljson --sdk-version 8.0.100
```

### Excelファイル操作のための依存関係

このプロジェクトではClosedXMLを使用してExcelファイルを操作します。依存関係は.csprojファイルに含まれているため、追加のインストールは必要ありません。

## 2. テスト環境のセットアップ

プロジェクトルートディレクトリで以下のコマンドを実行します:

```bash
./setup_mac_test_env.sh
```

このスクリプトは以下の処理を行います:
- .NET SDKのバージョン確認（8.0以上が必要）
- 必要なディレクトリの作成
- テスト用Excelファイルの準備
- プロジェクトのビルド
- 単体テストの実行

## 3. アプリケーションの実行

### 直接実行

```bash
dotnet run --project src/VacancyImport/VacancyImport.csproj
```

### Docker経由での実行

Dockerを使用する場合は以下のコマンドを実行します:

```bash
docker-compose up --build
```

または、テスト用のDockerコンテナを使用する場合:

```bash
./test_with_docker.sh
```

## 4. 開発のヒント

### 設定ファイル

開発環境用の設定は `src/VacancyImport/appsettings.Development.json` に記述されています。必要に応じて編集してください。

### デバッグ

Visual Studio for Macまたは Visual Studio Codeを使用してデバッグできます。

#### Visual Studio Codeの場合

1. C#拡張機能をインストール
2. F5キーでデバッグ開始

## 5. トラブルシューティング

### よくある問題

1. **ビルドエラー: NETSDK1045**
   ```
   error NETSDK1045: 現在の .NET SDK は、ターゲットとする .NET 8.0 をサポートしていません。
   ```
   - 解決方法: .NET 8.0 SDKをインストールしてください。上記の「.NET SDKのインストール」セクションを参照。

2. **ビルドエラー: その他**
   - NuGetパッケージの復元を試してください: `dotnet restore`

3. **Excelファイル操作エラー**
   - テストデータディレクトリが正しく設定されているか確認してください

4. **実行時エラー**
   - ログファイルを確認: `logs/` ディレクトリ内のログファイル

問題が解決しない場合は、issueを作成してください。 