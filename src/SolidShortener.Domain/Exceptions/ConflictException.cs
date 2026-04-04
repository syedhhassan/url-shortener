namespace SolidShortener.Domain.Entities;

public class ConflictException(string message) : Exception(message);
