namespace Ged.Application.Common;

public sealed class NotFoundException(string what, Guid id)
    : Exception($"{what} introuvable : {id}.");
