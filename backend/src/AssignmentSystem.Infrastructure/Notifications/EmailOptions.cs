namespace AssignmentSystem.Infrastructure.Notifications;

/// <summary>
/// SMTP and outbox settings, bound from the <c>Email</c> configuration section (which reads
/// its values from environment variables — see <c>.env.example</c>). Nothing here has a real
/// credential as a default: an unconfigured checkout runs with delivery disabled rather than
/// pretending to send.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>SMTP host. Empty disables delivery (notifications are logged instead).</summary>
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    /// <summary>STARTTLS. On by default — plaintext SMTP should be an explicit choice.</summary>
    public bool UseSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>Envelope sender. Falls back to <see cref="Username"/> when unset, since for
    /// most providers they are the same address and a mismatch is rejected anyway.</summary>
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Assignment System";

    /// <summary>How long to wait on the mail server before treating an attempt as failed.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    // ── Outbox dispatch ───────────────────────────────────────────────────────

    /// <summary>Seconds between dispatcher sweeps of the pending queue.</summary>
    public int DispatchIntervalSeconds { get; set; } = 30;

    /// <summary>Rows taken per sweep — bounds how long one pass can hold a DB scope open.</summary>
    public int BatchSize { get; set; } = 25;

    /// <summary>
    /// Attempts before a notification is marked Failed and left for an admin to retry.
    /// Three covers the transient cases (a brief network blip, a rate limit) without
    /// hammering a server that is genuinely misconfigured.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay before a failed notification is retried; it doubles with each further
    /// failure. Thirty seconds means a transient blip costs the recipient almost nothing,
    /// while a server that is genuinely down is not hit once per sweep until the attempt
    /// budget is gone.
    /// </summary>
    public int RetryBackoffSeconds { get; set; } = 30;

    /// <summary>
    /// How long a claimed row may stay claimed before another sweep may take it back. This
    /// is the recovery window after a dispatcher is killed mid-batch: the rows it held are
    /// stuck until this elapses, so it wants to be comfortably longer than a sweep can
    /// legitimately take, and short enough that a restart is not visibly delayed.
    /// </summary>
    public int ClaimTimeoutSeconds { get; set; } = 300;

    /// <summary>Frontend base URL used for the "open it here" link in email bodies.</summary>
    public string AppBaseUrl { get; set; } = string.Empty;

    /// <summary>The address mail is actually sent from, after the username fallback.</summary>
    public string EffectiveFromAddress =>
        string.IsNullOrWhiteSpace(FromAddress) ? Username : FromAddress;

    /// <summary>
    /// A host and a sender are the minimum needed to send anything. Credentials are not
    /// required — a local relay or MailHog needs none.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(EffectiveFromAddress);
}
