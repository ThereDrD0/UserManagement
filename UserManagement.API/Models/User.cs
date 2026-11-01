namespace UserManagement.API.Models;

public sealed record User(
    Guid UserId, 
    string CountryIsoCode, 
    string Locale,
    DateTime DateCreated, 
    DateTime? DateModified);