// See https://aka.ms/new-console-template for more information

using System;
using ClosedXML.Excel;
using Newtonsoft.Json;
using Npgsql;
using Supabase;

namespace ReservationSyncTool;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("予約管理システム連携ツール - 開発環境テスト");
        Console.WriteLine($".NET Version: {Environment.Version}");
        Console.WriteLine($"OS Platform: {Environment.OSVersion.Platform}");
        Console.WriteLine($"OS Version: {Environment.OSVersion.VersionString}");

        // ライブラリバージョン確認
        Console.WriteLine($"ClosedXML Version: {typeof(XLWorkbook).Assembly.GetName().Version}");
        Console.WriteLine($"Newtonsoft.Json Version: {typeof(JsonConvert).Assembly.GetName().Version}");
        Console.WriteLine($"Npgsql Version: {typeof(NpgsqlConnection).Assembly.GetName().Version}");
        Console.WriteLine($"Supabase Version: {typeof(Client).Assembly.GetName().Version}");

        Console.WriteLine("\n開発環境が正常に構築されました！");
    }
}