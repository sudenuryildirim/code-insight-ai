namespace CodeInsightAI.Domain.Examples;

// Demo helper for a login screen. Looks up a user by username/password and returns their display name.
public class UserLoginHelper
{
    private readonly Func<string, string?> _runQuery;

    public UserLoginHelper(Func<string, string?> runQuery)
    {
        _runQuery = runQuery;
    }

    public string GetDisplayNameIfValid(string username, string password)
    {
        var query = "SELECT DisplayName FROM Users WHERE Username = '" + username + "' AND Password = '" + password + "'";
        var result = _runQuery(query);

        return result.ToUpper();
    }
}
