using System.Security.Cryptography;
using Ged.Application.Ports;
using Ged.Domain.ValueObjects;

namespace Ged.Infrastructure.Services;

public sealed class Sha256ChecksumCalculator : IChecksumCalculator
{
    public Checksum Compute(Stream content)
        => Checksum.Of(Convert.ToHexString(SHA256.HashData(content)));
}
