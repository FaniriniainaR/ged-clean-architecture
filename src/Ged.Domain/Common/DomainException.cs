namespace Ged.Domain.Common;

/// <summary>Violation d'une règle métier.</summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public sealed class InvalidValueException(string message) : DomainException(message);

public sealed class UnsupportedFileTypeException(string fileName)
    : DomainException($"Type de fichier non autorisé : '{fileName}'.");

public sealed class FileTooLargeException(long size, long max)
    : DomainException($"Fichier trop volumineux : {size} octets (maximum {max}).");

public sealed class DuplicateVersionException()
    : DomainException("Ce fichier est identique à la version courante.");

public sealed class DuplicateTitleException(string title)
    : DomainException($"Un document nommé '{title}' existe déjà dans ce dossier.");

public sealed class DocumentTrashedException()
    : DomainException("Le document est dans la corbeille.");

public sealed class DocumentNotTrashedException()
    : DomainException("Le document n'est pas dans la corbeille.");

public sealed class FolderNotEmptyException(string name)
    : DomainException($"Le dossier '{name}' n'est pas vide.");
