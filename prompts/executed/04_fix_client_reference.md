# Clientの参照の曖昧さの解決

## 問題
`Supabase.Gotrue.Client`と`Supabase.Client`の間で`Client`の参照が曖昧になっています。

## 修正内容
1. 必要な`using`ディレクティブを追加
2. 完全修飾名を使用して参照を明確化

## 修正手順
1. `SupabaseService.cs`を開く
2. 以下の`using`ディレクティブを追加：
   ```csharp
   using SupabaseClient = Supabase.Client;
   using GotrueClient = Supabase.Gotrue.Client;
   ```
3. `Client`の参照を完全修飾名に変更
4. プロジェクトを再ビルドして確認

## 期待される結果
- `Client`の参照の曖昧さが解消される
- ビルドが正常に完了する
- コードが正しく動作する 