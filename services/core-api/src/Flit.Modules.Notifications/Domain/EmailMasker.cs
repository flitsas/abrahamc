namespace Flit.Modules.Notifications.Domain;

/// <summary>
/// Helper para enmascarar emails y telefonos antes de loguearlos en
/// notification_delivery.recipient_masked (Habeas Data — no persistir PII bruta).
/// </summary>
public static class ContactMasker
{
    public static string MaskEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return "***";
        var parts = email.Split('@', 2);
        var local = parts[0].Length <= 2 ? "**" : $"{parts[0][..1]}**{parts[0][^1]}";
        return $"{local}@{parts[1]}";
    }

    public static string MaskPhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Length < 4) return "***";
        return $"***{phone[^4..]}";
    }
}
