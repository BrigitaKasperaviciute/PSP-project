using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using POS_System.Api.Configurations;
using POS_System.Api.ExceptionHandler;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Logger;
using POS_System.Common.Exceptions;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;

namespace POS_System.IntegrationTests;

public class ApiInfrastructureCoverageTests
{
    [Fact]
    public void AddApiServices_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var result = services.AddApiServices(configuration);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddGlobalExceptionHandler_RegistersHandler()
    {
        var builder = WebApplication.CreateBuilder();

        builder.AddGlobalExceptionHandler();
        using var app = builder.Build();
        var handlers = app.Services.GetServices<IExceptionHandler>();

        Assert.Contains(handlers, handler => handler is GlobalExceptionHandler);
    }

    [Fact]
    public void ConfigureSwagger_ReturnsBuilder()
    {
        var builder = WebApplication.CreateBuilder();

        var result = builder.ConfigureSwagger();

        Assert.Same(builder, result);
    }

    [Fact]
    public void ConfigureValidators_ReturnsBuilder()
    {
        var builder = WebApplication.CreateBuilder();

        var result = builder.ConfigureValidators();

        Assert.Same(builder, result);
    }

    [Fact]
    public async Task GlobalExceptionHandler_HandlesGeneralException()
    {
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.ContentType = "application/json";

        var handled = await handler.TryHandleAsync(context, new Exception("boom"), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("boom", body);
    }

    [Fact]
    public async Task GlobalExceptionHandler_HandlesBaseException()
    {
        var handler = new GlobalExceptionHandler();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new NotFoundException("not found");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    [Fact]
    public async Task RequestLoggingMiddleware_HandlesRequestWithBody()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, NullLogger<ApplicationLogger>.Instance);
        var context = new DefaultHttpContext();
        var requestBody = Encoding.UTF8.GetBytes("""{"password":"secret"}""");
        context.Request.Body = new MemoryStream(requestBody);
        context.Request.ContentLength = requestBody.Length;

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task RequestLoggingMiddleware_HandlesRequestWithoutBody()
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, NullLogger<ApplicationLogger>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Body = new MemoryStream();
        context.Request.ContentLength = 0;

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }
}
