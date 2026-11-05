namespace Ohmline.Mobile.Core.Charging;

/// <summary>
/// What became of the driver's input: a value the app can send, or a sentence that says what
/// is wrong with it. The app's own type: the server has its own rules and its own results, and
/// the two never meet (section 19.5).
/// </summary>
public union ParseResult<T>(Parsed<T>, Invalid);

/// <summary>The input is valid; here is the value.</summary>
public sealed record Parsed<T>(T Value);

/// <summary>The input is not valid; tell the driver this.</summary>
public sealed record Invalid(string Message);
