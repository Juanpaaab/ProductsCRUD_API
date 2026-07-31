using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using ProductsCRUD_API.Dtos;

namespace ProductsCRUD_API.Controllers;

[ApiController]
[Route("api/github")]
public class GitHubController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<IActionResult> GetUser(string username)
    {
        var client = httpClientFactory.CreateClient("GitHub");
        var response = await client.GetAsync($"users/{username}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }

        response.EnsureSuccessStatusCode();

        var user = await response.Content.ReadFromJsonAsync<GitHubUserDto>();
        return Ok(user);
    }
}
