using Ged.Domain.ValueObjects;

namespace Ged.Application.Ports;

public interface IChecksumCalculator
{
    Checksum Compute(Stream content);
}
