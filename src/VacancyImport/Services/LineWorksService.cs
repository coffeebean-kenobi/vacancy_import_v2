using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VacancyImport.Configuration;

namespace VacancyImport.Services;

/// <summary>
/// LINE WORKS通知サービス
/// </summary>
public class LineWorksService : ILineWorksService
{
    private readonly LineWorksSettings _settings;
    private readonly ILogger<LineWorksService> _logger;
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    private DateTime _tokenExpiry;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public LineWorksService(IOptions<AppSettings> settings, ILogger<LineWorksService> logger, HttpClient httpClient)
    {
        _settings = settings.Value.LineWorksSettings;
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// アクセストークンを取得
    /// </summary>
    public async Task<string> GetAccessTokenAsync()
    {
        try
        {
            // トークンが有効な場合は再利用
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
            {
                return _accessToken;
            }

            var tokenRequest = new
            {
                grant_type = "client_credentials",
                client_id = _settings.ClientId,
                client_secret = _settings.ClientSecret
            };

            var response = await _httpClient.PostAsJsonAsync(_settings.TokenUrl, tokenRequest);
            response.EnsureSuccessStatusCode();

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (tokenResponse == null)
            {
                throw new Exception("トークン応答の解析に失敗しました");
            }

            _accessToken = tokenResponse.AccessToken;
            _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 300); // 5分前に期限切れとみなす

            _logger.LogInformation("新しいアクセストークンを取得しました");
            return _accessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "アクセストークンの取得中にエラーが発生しました");
            throw;
        }
    }

    /// <summary>
    /// 通知メッセージを送信
    /// </summary>
    public async Task SendNotificationAsync(string message)
    {
        try
        {
            var messageObj = new
            {
                content = new
                {
                    type = "text",
                    text = message
                }
            };

            await SendMessageAsync(messageObj);
            _logger.LogInformation("通知メッセージを送信しました: {Message}", message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知メッセージの送信中にエラーが発生しました");
            throw;
        }
    }

    /// <summary>
    /// プルーフリスト用の詳細通知を送信
    /// </summary>
    /// <param name="summary">サマリー文字列</param>
    /// <param name="proofListFileName">プルーフリストファイル名</param>
    public async Task SendProofListNotificationAsync(string summary, string proofListFileName)
    {
        try
        {
            var message = new
            {
                content = new
                {
                    type = "text",
                    text = $"📊 予約データ更新通知\n\n{summary}\n\n📄 証跡ファイル: {proofListFileName}\n⏰ 処理時刻: {DateTime.Now:yyyy/MM/dd HH:mm:ss}"
                }
            };

            await SendMessageAsync(message);
            _logger.LogInformation("プルーフリスト通知を送信しました");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "プルーフリスト通知の送信に失敗しました");
            throw;
        }
    }

    /// <summary>
    /// エラー通知を送信
    /// </summary>
    /// <param name="errorMessage">エラーメッセージ</param>
    /// <param name="errorCount">連続エラー回数</param>
    public async Task SendErrorNotificationAsync(string errorMessage, int errorCount)
    {
        try
        {
            var urgencyEmoji = errorCount >= 3 ? "🚨" : "⚠️";
            var message = new
            {
                content = new
                {
                    type = "text",
                    text = $"{urgencyEmoji} 予約システム連携エラー\n\n" +
                           $"エラー内容: {errorMessage}\n" +
                           $"連続エラー回数: {errorCount}\n" +
                           $"発生時刻: {DateTime.Now:yyyy/MM/dd HH:mm:ss}\n\n" +
                           (errorCount >= 3 ? "⚠️ 管理者による確認が必要です" : "")
                }
            };

            await SendMessageAsync(message);
            _logger.LogInformation($"エラー通知を送信しました (エラー回数: {errorCount})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "エラー通知の送信に失敗しました");
            // エラー通知の失敗は例外を再スローしない
        }
    }

    /// <summary>
    /// 共通のメッセージ送信処理
    /// </summary>
    private async Task SendMessageAsync(object messageObj)
    {
        var token = await GetAccessTokenAsync();
        var url = string.Format(_settings.MessageUrl, _settings.BotId);

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(messageObj),
                Encoding.UTF8,
                "application/json"
            )
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// トークン応答モデル
    /// </summary>
    private class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
} 