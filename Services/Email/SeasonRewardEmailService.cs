using System.Net;
using Eliteracingleague.API.Constants;
using Eliteracingleague.API.Data;
using Eliteracingleague.API.Models;
using Eliteracingleague.API.Services.SystemTime;
using Microsoft.EntityFrameworkCore;

namespace Eliteracingleague.API.Services.Email;

/// <summary>
/// Builds and sends all emails related to season rewards.
/// Email failure never rolls back the reward workflow; the caller receives false
/// and the failure is written to the application log.
/// </summary>
public sealed class SeasonRewardEmailService
{
    private readonly EliteRacingLeagueContext _context;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SeasonRewardEmailService> _logger;

    public SeasonRewardEmailService(
        EliteRacingLeagueContext context,
        IEmailService emailService,
        IConfiguration configuration,
        IDateTimeProvider dateTimeProvider,
        ILogger<SeasonRewardEmailService> logger)
    {
        _context = context;
        _emailService = emailService;
        _configuration = configuration;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<bool> TrySendAwardedAsync(
        int rewardId,
        CancellationToken cancellationToken = default)
    {
        var reward = await LoadRewardAsync(rewardId, cancellationToken);
        if (reward == null)
        {
            _logger.LogWarning("Cannot send reward-awarded email because reward {RewardId} was not found.", rewardId);
            return false;
        }

        var hasPhysicalGift = reward.RewardItem != null;
        var claimUrl = BuildClaimUrl(reward.SeasonRewardId);
        var deadline = FormatLocalDateTime(reward.ClaimDeadline);
        var rewardDetails = BuildRewardDetails(reward);

        var actionBlock = hasPhysicalGift
            ? $"""
                <p style="margin:16px 0 8px;">Để chúng tôi giao quà, bạn cần đăng nhập và gửi:</p>
                <ul style="margin-top:0;">
                    <li>Họ và tên người nhận</li>
                    <li>Số điện thoại</li>
                    <li>Địa chỉ giao quà đầy đủ</li>
                </ul>
                <p style="margin:16px 0;">
                    <a href="{E(claimUrl)}" style="display:inline-block;background:#1f4b73;color:#fff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:700;">
                        Gửi thông tin nhận quà
                    </a>
                </p>
                <p><strong>Hạn gửi thông tin:</strong> {E(deadline)}</p>
                """
            : $"""
                <p style="margin:16px 0;">Phần thưởng này không có quà hiện vật. Điểm thưởng sẽ được xử lý theo quy định của Season tiếp theo.</p>
                <p style="margin:16px 0;">
                    <a href="{E(claimUrl)}" style="display:inline-block;background:#1f4b73;color:#fff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:700;">
                        Xem phần thưởng của tôi
                    </a>
                </p>
                """;

        var subject = $"Chúc mừng bạn đạt hạng #{reward.RankPosition} - {reward.Season.SeasonName}";
        var body = BuildLayout(
            "Chúc mừng thành tích của bạn!",
            $"""
            <p>Xin chào <strong>{E(reward.Spectator.FullName)}</strong>,</p>
            <p>Bạn đã hoàn thành <strong>{E(reward.Season.SeasonName)}</strong> ở vị trí <strong>hạng #{reward.RankPosition}</strong>.</p>
            {rewardDetails}
            {actionBlock}
            <p style="font-size:13px;color:#666;">Vui lòng không gửi mật khẩu hoặc mã OTP qua email. Thông tin giao quà nên được khai báo trực tiếp trong hệ thống.</p>
            """);

        return await TrySendAsync(reward, "Awarded", subject, body);
    }

    public async Task<bool> TrySendClaimReceivedAsync(
        int rewardId,
        CancellationToken cancellationToken = default)
    {
        var reward = await LoadRewardAsync(rewardId, cancellationToken);
        if (reward == null)
        {
            _logger.LogWarning("Cannot send reward-claim email because reward {RewardId} was not found.", rewardId);
            return false;
        }

        var addressBlock = reward.RewardItem == null
            ? string.Empty
            : $"""
              <div style="background:#f6f7f9;border-radius:8px;padding:14px 16px;margin:16px 0;">
                  <p style="margin:4px 0;"><strong>Người nhận:</strong> {E(reward.ReceiverName)}</p>
                  <p style="margin:4px 0;"><strong>Số điện thoại:</strong> {E(reward.ReceiverPhone)}</p>
                  <p style="margin:4px 0;"><strong>Địa chỉ:</strong> {E(reward.DeliveryAddress)}</p>
              </div>
              """;

        var subject = $"Đã nhận thông tin nhận quà - {reward.Season.SeasonName}";
        var body = BuildLayout(
            "Thông tin nhận quà đã được ghi nhận",
            $"""
            <p>Xin chào <strong>{E(reward.Spectator.FullName)}</strong>,</p>
            <p>Hệ thống đã nhận yêu cầu nhận phần thưởng <strong>{E(reward.RewardName)}</strong>.</p>
            {addressBlock}
            <p>Admin sẽ kiểm tra thông tin và cập nhật trạng thái quà trong hệ thống. Bạn sẽ nhận thêm email khi quà được duyệt, chuẩn bị hoặc giao thành công.</p>
            """);

        return await TrySendAsync(reward, "Claimed", subject, body);
    }

    public async Task<bool> TrySendStatusUpdatedAsync(
        int rewardId,
        CancellationToken cancellationToken = default)
    {
        var reward = await LoadRewardAsync(rewardId, cancellationToken);
        if (reward == null)
        {
            _logger.LogWarning("Cannot send reward-status email because reward {RewardId} was not found.", rewardId);
            return false;
        }

        var (title, statusMessage) = reward.Status switch
        {
            SeasonRewardStatuses.Approved => ("Phần thưởng đã được duyệt", "Thông tin nhận quà của bạn đã được Admin duyệt."),
            SeasonRewardStatuses.Preparing => ("Phần thưởng đang được chuẩn bị", "Quà của bạn đang được đóng gói và chuẩn bị giao."),
            SeasonRewardStatuses.Delivered => ("Phần thưởng đã được giao", "Hệ thống đã cập nhật phần thưởng của bạn là đã giao."),
            SeasonRewardStatuses.Rejected => ("Yêu cầu nhận quà chưa được chấp nhận", "Yêu cầu nhận quà đã bị từ chối. Vui lòng xem ghi chú của Admin và liên hệ hỗ trợ nếu cần."),
            _ => ("Trạng thái phần thưởng đã thay đổi", $"Trạng thái mới của phần thưởng là {reward.Status}.")
        };

        var noteBlock = string.IsNullOrWhiteSpace(reward.AdminNote)
            ? string.Empty
            : $"""
              <div style="background:#fff7e6;border-left:4px solid #d99a22;padding:12px 14px;margin:16px 0;">
                  <strong>Ghi chú từ Admin:</strong><br />{E(reward.AdminNote)}
              </div>
              """;

        var subject = $"{title} - {reward.RewardName}";
        var body = BuildLayout(
            title,
            $"""
            <p>Xin chào <strong>{E(reward.Spectator.FullName)}</strong>,</p>
            <p>{E(statusMessage)}</p>
            {BuildRewardDetails(reward)}
            {noteBlock}
            <p><a href="{E(BuildClaimUrl(reward.SeasonRewardId))}">Mở trang phần thưởng của tôi</a></p>
            """);

        return await TrySendAsync(reward, $"Status:{reward.Status}", subject, body);
    }

    private async Task<SeasonReward?> LoadRewardAsync(
        int rewardId,
        CancellationToken cancellationToken)
    {
        return await _context.SeasonRewards
            .AsNoTracking()
            .Include(item => item.Season)
            .Include(item => item.Spectator)
            .Include(item => item.RewardItem)
            .FirstOrDefaultAsync(item => item.SeasonRewardId == rewardId, cancellationToken);
    }

    private async Task<bool> TrySendAsync(
        SeasonReward reward,
        string emailType,
        string subject,
        string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(reward.Spectator.Email))
        {
            _logger.LogWarning(
                "Reward email {EmailType} was skipped because spectator {SpectatorId} has no email.",
                emailType,
                reward.SpectatorId);
            return false;
        }

        try
        {
            await _emailService.SendEmailAsync(reward.Spectator.Email, subject, htmlBody);
            _logger.LogInformation(
                "Reward email {EmailType} sent for reward {RewardId} to spectator {SpectatorId}.",
                emailType,
                reward.SeasonRewardId,
                reward.SpectatorId);
            return true;
        }
        catch (Exception ex)
        {
            // Reward creation/status update must remain successful even when SMTP is temporarily unavailable.
            _logger.LogError(
                ex,
                "Failed to send reward email {EmailType} for reward {RewardId} to {Email}.",
                emailType,
                reward.SeasonRewardId,
                reward.Spectator.Email);
            return false;
        }
    }

    private string BuildClaimUrl(int rewardId)
    {
        var baseUrl = (_configuration["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var claimPath = (_configuration["Frontend:RewardClaimPath"] ?? "/spectator/results").Trim();
        if (!claimPath.StartsWith('/')) claimPath = "/" + claimPath;
        return $"{baseUrl}{claimPath}?rewardId={rewardId}";
    }

    private string FormatLocalDateTime(DateTime? utcValue)
    {
        if (!utcValue.HasValue) return "Không giới hạn";

        var utc = DateTime.SpecifyKind(utcValue.Value, DateTimeKind.Utc);
        var zone = SystemDateTimeProvider.ResolveTimeZone(_dateTimeProvider.TimeZoneId);
        var local = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        return $"{local:dd/MM/yyyy HH:mm} ({_dateTimeProvider.TimeZoneId})";
    }

    private static string BuildRewardDetails(SeasonReward reward)
    {
        var itemText = reward.RewardItem == null
            ? "Không có quà hiện vật"
            : $"{reward.RewardItem.Name} x {reward.Quantity}";

        var description = string.IsNullOrWhiteSpace(reward.RewardDescription)
            ? string.Empty
            : $"<p style=\"margin:6px 0;\"><strong>Mô tả:</strong> {E(reward.RewardDescription)}</p>";

        return $"""
            <div style="background:#f6f7f9;border-radius:8px;padding:14px 16px;margin:16px 0;">
                <p style="margin:4px 0;"><strong>Phần thưởng:</strong> {E(reward.RewardName)}</p>
                <p style="margin:4px 0;"><strong>Quà hiện vật:</strong> {E(itemText)}</p>
                <p style="margin:4px 0;"><strong>Điểm thưởng Season sau:</strong> {reward.BonusPoints}</p>
                <p style="margin:4px 0;"><strong>Điểm chốt Season:</strong> {reward.FinalPoints}</p>
                {description}
            </div>
            """;
    }

    private static string BuildLayout(string heading, string content)
    {
        return $"""
            <!doctype html>
            <html lang="vi">
            <head><meta charset="utf-8" /></head>
            <body style="margin:0;background:#f2f3f5;font-family:Arial,Helvetica,sans-serif;color:#20242a;">
                <div style="max-width:680px;margin:24px auto;background:#ffffff;border-radius:12px;overflow:hidden;border:1px solid #e3e5e8;">
                    <div style="background:#173b5e;color:#ffffff;padding:22px 28px;">
                        <div style="font-size:13px;letter-spacing:1px;text-transform:uppercase;opacity:.85;">Elite Racing League</div>
                        <h1 style="font-size:24px;margin:8px 0 0;">{E(heading)}</h1>
                    </div>
                    <div style="padding:26px 28px;line-height:1.6;">{content}</div>
                    <div style="padding:16px 28px;background:#f7f8fa;color:#6b7280;font-size:12px;">
                        Email được gửi tự động từ Elite Racing League. Vui lòng không chia sẻ mật khẩu hoặc mã OTP.
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
