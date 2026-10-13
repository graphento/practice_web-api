using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests;

public class UsersApiRegressionTests : UsersApiTestsBase
{
    [TestCase("application/json", HttpStatusCode.OK)]
    [TestCase("application/xml", HttpStatusCode.OK)]
    [TestCase("text/plain", HttpStatusCode.NotAcceptable)]
    public async Task HeadUsesGetContentNegotiationWithoutBody(string accept, HttpStatusCode status)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head,
            BuildUsersByIdUri("77777777-7777-7777-7777-777777777777"));
        request.Headers.Add("Accept", accept);
        using var response = await HttpClient.SendAsync(request);

        response.StatusCode.Should().Be(status);
        response.ReadContentAsBytes().Should().BeEmpty();
        if (status == HttpStatusCode.OK)
            response.ShouldHaveHeader("Content-Type", accept + "; charset=utf-8");
    }

    [Test]
    public async Task PaginationLinksLeadToNextAndPreviousPages()
    {
        var id = await CreateUser(new { login = "zzLastUser" });
        using var first = await HttpClient.GetAsync(BuildUsersWithPagesUri(1, 1));
        var pagination = JObject.Parse(first.GetRequiredHeader("X-Pagination").Single());
        first.ReadContentAsJson().Count().Should().Be(1);
        pagination.Value<int>("totalCount").Should().Be(2);
        pagination.Value<int>("totalPages").Should().Be(2);

        using var last = await HttpClient.GetAsync(pagination.Value<string>("nextPageLink"));
        last.StatusCode.Should().Be(HttpStatusCode.OK);
        last.ReadContentAsJson()[0].Value<string>("id").Should().Be(id);
        var lastPagination = JObject.Parse(last.GetRequiredHeader("X-Pagination").Single());
        lastPagination.Value<string>("nextPageLink").Should().BeNull();
        using var previous = await HttpClient.GetAsync(lastPagination.Value<string>("previousPageLink"));
        previous.ShouldHaveJsonContentEquivalentTo(first.ReadContentAsJson());
    }

    [Test]
    public async Task VeryLargePageNumberReturnsEmptyPage()
    {
        using var response = await HttpClient.GetAsync(BuildUsersWithPagesUri(int.MaxValue, 20));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.ReadContentAsJson().Should().BeEmpty();
    }
}
