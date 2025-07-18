using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;
using VacancyImport.Configuration;
using VacancyImport.Services;

namespace VacancyImport.Tests.Services;

public class LineWorksServiceTests
{
    private readonly Mock<IOptions<AppSettings>> _mockOptions;
    private readonly Mock<ILogger<LineWorksService>> _mockLogger;
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly AppSettings _appSettings;

    public LineWorksServiceTests()
    {
        // 設定のセットアップ
        _appSettings = new AppSettings
        {
            LineWorksSettings = new LineWorksSettings
            {
                BotId = "test-bot-id",
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret",
                TokenUrl = "https://auth.worksmobile.com/oauth2/v2.0/token",
                MessageUrl = "https://www.worksapis.com/v1.0/bots/{0}/messages"
            }
        };

        _mockOptions = new Mock<IOptions<AppSettings>>();
        _mockOptions.Setup(x => x.Value).Returns(_appSettings);
        
        _mockLogger = new Mock<ILogger<LineWorksService>>();
        
        _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
    }

    [Fact]
    public async Task GetAccessTokenAsync_ReturnsToken()
    {
        // Arrange
        var tokenResponse = new
        {
            access_token = "test-access-token",
            expires_in = 3600
        };

        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse), Encoding.UTF8, "application/json")
            });

        var service = new LineWorksService(_mockOptions.Object, _mockLogger.Object, _httpClient);

        // Act
        var token = await service.GetAccessTokenAsync();

        // Assert
        Assert.Equal("test-access-token", token);
        
        // トークン取得のリクエストが正しく送信されたことを確認
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Post && 
                req.RequestUri.ToString() == _appSettings.LineWorksSettings.TokenUrl),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task SendNotificationAsync_SendsMessage()
    {
        // Arrange
        var tokenResponse = new
        {
            access_token = "test-access-token",
            expires_in = 3600
        };

        var messageResponse = new
        {
            message_id = "test-message-id"
        };

        _mockHttpMessageHandler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(tokenResponse), Encoding.UTF8, "application/json")
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(messageResponse), Encoding.UTF8, "application/json")
            });

        var service = new LineWorksService(_mockOptions.Object, _mockLogger.Object, _httpClient);
        var message = "テストメッセージ";

        // Act
        await service.SendNotificationAsync(message);

        // Assert
        // トークン取得のリクエストが送信されたことを確認
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.AtLeastOnce(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Post && 
                req.RequestUri.ToString() == _appSettings.LineWorksSettings.TokenUrl),
            ItExpr.IsAny<CancellationToken>()
        );

        // メッセージ送信のリクエストが送信されたことを確認
        var expectedMessageUrl = string.Format(_appSettings.LineWorksSettings.MessageUrl, _appSettings.LineWorksSettings.BotId);
        _mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Method == HttpMethod.Post && 
                req.RequestUri.ToString() == expectedMessageUrl &&
                req.Headers.Authorization != null &&
                req.Headers.Authorization.Scheme == "Bearer" &&
                req.Headers.Authorization.Parameter == "test-access-token"),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task GetAccessTokenAsync_ErrorResponse_ThrowsException()
    {
        // Arrange
        _mockHttpMessageHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Invalid request", Encoding.UTF8)
            });

        var service = new LineWorksService(_mockOptions.Object, _mockLogger.Object, _httpClient);

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetAccessTokenAsync());
    }
} 