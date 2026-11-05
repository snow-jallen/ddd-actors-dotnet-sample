using Microsoft.Extensions.Logging;
using Ohmline.DeviceGateway.Ocpp.Frames;
using Ohmline.SharedKernel.Identity;

namespace Ohmline.DeviceGateway.Diagnostics;

/// <summary>
/// The gateway's log messages, each written once (section 18.4), with an id and named
/// properties: <c>ChargePointId</c> on every one, so that one charger's conversation can be
/// found among thousands. Written at the moment of the call, inside the frame's span.
/// </summary>
internal static partial class GatewayLog
{
    [LoggerMessage(2001, LogLevel.Information, "{ChargePointId} booted on this gateway node")]
    public static partial void Booted(this ILogger log, ChargePointId chargePointId);

    [LoggerMessage(2002, LogLevel.Warning,
        "{ChargePointId}: no site or tariff for its sessions yet")]
    public static partial void TermsUnknown(
        this ILogger log, ChargePointId chargePointId, Exception cause);

    [LoggerMessage(2003, LogLevel.Warning,
        "{ChargePointId}: Charging refused a report: {Refusal} ({Code})")]
    public static partial void ReportRefused(
        this ILogger log, ChargePointId chargePointId, string refusal, string code);

    [LoggerMessage(2004, LogLevel.Warning, "{ChargePointId}: Charging did not answer")]
    public static partial void ChargingDidNotAnswer(
        this ILogger log, ChargePointId chargePointId, Exception cause);

    [LoggerMessage(2005, LogLevel.Warning, "{ChargePointId} sent a broken frame: {Error}")]
    public static partial void BrokenFrame(
        this ILogger log, ChargePointId chargePointId, FrameError error);

    [LoggerMessage(2006, LogLevel.Information, "{ChargePointId}: {Action} ignored. {Reason}")]
    public static partial void CallIgnored(
        this ILogger log, ChargePointId chargePointId, string action, string reason);

    [LoggerMessage(2007, LogLevel.Warning, "{ChargePointId}: {Action} malformed. {Reason}")]
    public static partial void CallMalformed(
        this ILogger log, ChargePointId chargePointId, string action, string reason);

    [LoggerMessage(2008, LogLevel.Information,
        "{ChargePointId} answered {MessageId}, which nobody is waiting for")]
    public static partial void UnexpectedAnswer(
        this ILogger log, ChargePointId chargePointId, string messageId);

    [LoggerMessage(2009, LogLevel.Warning,
        "{ChargePointId} sent nothing for {SilenceTimeout}; its connection is closed")]
    public static partial void Silent(
        this ILogger log, ChargePointId chargePointId, TimeSpan silenceTimeout);
}
