# 設定ガイド

## 概要

このドキュメントでは、VacancyImportツールの設定方法について説明します。設定は以下の方法で行うことができます：

1. 設定ファイル（appsettings.json）
2. 環境別設定ファイル（appsettings.[Environment].json）
3. 環境変数

## 設定ファイル

アプリケーションは以下の順序で設定を読み込みます：

1. appsettings.json（基本設定）
2. appsettings.[Environment].json（環境固有の設定）
3. 環境変数（VACANCY_IMPORT_プレフィックス付き）

環境変数は他の設定をオーバーライドします。

## 環境変数の設定例

### 基本設定

```bash
# 実行環境の指定（Development, Staging, Production）
export ASPNETCORE_ENVIRONMENT=Development

# Excelの設定
export VACANCY_IMPORT_ExcelSettings__PollingIntervalMinutes=15
export VACANCY_IMPORT_ExcelSettings__RetryCount=3

# 環境別Excelパス設定
export VACANCY_IMPORT_ExcelSettings__Environments__Development__BasePath=./test_data/excel
export VACANCY_IMPORT_ExcelSettings__Environments__Production__BasePath=\\\\192.168.200.20\\全社共有\\SS予約表\\2025年
```

### Supabase設定

```bash
export VACANCY_IMPORT_SupabaseSettings__Url=https://your-project.supabase.co
export VACANCY_IMPORT_SupabaseSettings__Key=your-supabase-key
export VACANCY_IMPORT_SupabaseSettings__TableName=room_availability
```

### LINE WORKS設定

```bash
export VACANCY_IMPORT_LineWorksSettings__BotId=your-bot-id
export VACANCY_IMPORT_LineWorksSettings__ClientId=your-client-id
export VACANCY_IMPORT_LineWorksSettings__ClientSecret=your-client-secret
```

### ロギング設定

```bash
export VACANCY_IMPORT_Logging__LogLevel__Default=Information
export VACANCY_IMPORT_Logging__LogLevel__Microsoft=Warning
export VACANCY_IMPORT_Logging__LogFilePath=logs/vacancy-import-{Date}.log
```

## 設定ファイルの暗号化

機密情報を含む設定ファイルは、SecurityManagerクラスを使用して暗号化できます。

```csharp
// 暗号化の例
var securityManager = serviceProvider.GetRequiredService<SecurityManager>();
var encryptedValue = securityManager.EncryptValue("your-secret-value");

// 復号の例
var decryptedValue = securityManager.DecryptValue(encryptedValue);
```

## 設定の検証

アプリケーション起動時に、ConfigurationValidatorによって設定値の検証が行われます。必須項目が欠けている場合や、値の範囲が適切でない場合はエラーが発生します。

## 設定の動的更新

アプリケーション実行中に設定ファイルを変更すると、自動的に変更が検出され適用されます。この機能は `ConfigurationManager` クラスによって提供されています。

```csharp
// 設定変更の監視例
configManager.ConfigurationChanged += (sender, args) => {
    Console.WriteLine($"設定が変更されました: {args.Name}");
    // 設定変更に応じた処理
};
```

## 設定変更の監査

設定変更はすべて `audit.log` ファイルに記録されます。このログには変更日時、変更したユーザー、変更内容が含まれます。

```
Timestamp|UserId|Action|Resource
2023-05-01T12:34:56.7890000Z|system|startup|application
2023-05-01T12:35:00.1234000Z|admin|config_change|appsettings.json
``` 