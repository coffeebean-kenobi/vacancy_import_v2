# アセンブリ属性の重複問題の修正

## 問題
`VacancyImport.csproj`でアセンブリ属性が重複して定義されているため、ビルドエラーが発生しています。

## 修正内容
1. `VacancyImport.csproj`から重複するアセンブリ属性を削除します。
2. 以下の属性を削除対象とします：
   - AssemblyCompanyAttribute
   - AssemblyConfigurationAttribute
   - AssemblyFileVersionAttribute
   - AssemblyInformationalVersionAttribute
   - AssemblyProductAttribute
   - AssemblyTitleAttribute
   - AssemblyVersionAttribute
   - TargetFrameworkAttribute

## 修正手順
1. `src/VacancyImport/VacancyImport.csproj`を開く
2. 重複するアセンブリ属性の定義を削除
3. プロジェクトを再ビルドして確認

## 期待される結果
- アセンブリ属性の重複エラーが解消される
- ビルドが正常に完了する 