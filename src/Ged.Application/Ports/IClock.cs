namespace Ged.Application.Ports;

/// <summary>Horloge injectée : permet de simuler « 31 jours plus tard » dans les tests.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
