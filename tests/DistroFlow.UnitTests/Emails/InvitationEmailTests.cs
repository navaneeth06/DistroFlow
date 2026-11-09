using DistroFlow.Application.Emails;

namespace DistroFlow.UnitTests.Emails;

public class InvitationEmailTests
{
    private const string Link = "https://app.example.com/set-password?userId=42&token=abc%2B1";

    [Fact]
    public void Contains_recipient_distributor_name_and_link()
    {
        var email = InvitationEmail.Create("anita@example.com", "Anita Rao", "Sharma Traders", Link);

        Assert.Equal("anita@example.com", email.To);
        Assert.Contains("Sharma Traders", email.Subject);
        Assert.Contains("Anita Rao", email.HtmlBody);
        Assert.Contains(Link, email.TextBody);
        Assert.Contains("https://app.example.com/set-password?userId=42&amp;token=abc%2B1", email.HtmlBody);
    }

    [Fact]
    public void User_supplied_names_are_html_encoded()
    {
        var email = InvitationEmail.Create(
            "anita@example.com", "<script>alert(1)</script>", "<b>Evil</b> Traders", Link);

        Assert.DoesNotContain("<script>", email.HtmlBody);
        Assert.DoesNotContain("<b>Evil</b>", email.HtmlBody);
        Assert.Contains("&lt;script&gt;", email.HtmlBody);
        Assert.Contains("&lt;b&gt;Evil&lt;/b&gt;", email.HtmlBody);
    }
}
