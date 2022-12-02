
using Microsoft.AspNetCore.RateLimiting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.Diagnostics.CodeAnalysis;
using System.Net.Mime;
using System.Text;
using System.Threading.RateLimiting;
using Image = SixLabors.ImageSharp.Image;

namespace Net7samples
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            #region Rate limiting

            builder.Services.AddRateLimiter(_ => _
                .AddFixedWindowLimiter(policyName: "fixed", options =>
                {
                    options.PermitLimit = 4;
                    options.Window = TimeSpan.FromSeconds(12);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 2;
                }));

            #endregion

            var app = builder.Build();

            app.UseRateLimiter();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            #region Endpoint filters & Rate limiting

            app.MapGet("/rum/{brand}", (string brand, HttpContext httpContext) =>
            {
                var forecast = Enumerable.Range(1, 5).Select(index =>
                    new WeatherForecast
                    {
                        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        TemperatureC = Random.Shared.Next(-20, 55),
                        Summary = summaries[Random.Shared.Next(summaries.Length)]
                    })
                    .ToArray();
                return forecast;
            })
            .AddEndpointFilter<AuditFilter>()
            .AddEndpointFilter(async (context, next) =>
            {
                var brand = context.GetArgument<string>(0);

                if (brand.Equals("bozkov"))
                {
                    return Results.BadRequest("Bozkov is not a rum");
                }

                return await next(context);
            })
            .RequireRateLimiting("fixed")
            .WithName("GetBottlings")
            .WithOpenApi();            

            #endregion

            #region Results.Stream

            /// We introduced new Results.Stream overloads to accommodate scenarios that need access to the underlying HTTP response stream without buffering. 
            /// These overloads also improve cases where an API streams data to the HTTP response stream, like from Azure Blob Storage.
            app.MapGet("/rum/{brand}/image", (string brand, HttpContext http, CancellationToken token) =>
            {
                http.Response.Headers.CacheControl = $"public,max-age={TimeSpan.FromHours(24).TotalSeconds}";

                // Returns bufferless stream
                return Results.Stream(stream => ResizeImageAsync(brand, stream, token), "image/jpeg");
            })
            .WithName("GetBrandImage")
            .WithOpenApi();

            async Task ResizeImageAsync(string brand, Stream stream, CancellationToken token)
            {
                var strPath = $"{brand}.jpg";
                using var image = await Image.LoadAsync(strPath, token);
                int width = image.Width / 20;
                int height = image.Height / 20;
                image.Mutate(x => x.Resize(width, height));
                await image.SaveAsync(stream, JpegFormat.Instance, cancellationToken: token);
            }

            #endregion

            #region Raw string literals & Custom result type & WithOpenApi

            app.MapGet("/html", () => Results.Extensions.Html($"""
                <!doctype html>
                <html>
                    <head><title>Rum</title></head>
                    <body>
                        <h1>Hello Rum World</h1>
                        <p>The time on the server is {DateTime.Now:O}</p>
                    </body>
                </html>
            """))
            .WithOpenApi(generatedOperation =>
            {
                generatedOperation.Description = "Returns pointless HTML";
                return generatedOperation;
            })
            .CacheOutput(); // => bum, cached :D

            #endregion

            app.Run();
        }
    }

    public class AuditFilter : IEndpointFilter
    {
        protected readonly ILogger Logger;
        private readonly string _methodName;

        public AuditFilter(ILoggerFactory loggerFactory)
        {
            Logger = loggerFactory.CreateLogger<AuditFilter>();
            _methodName = GetType().Name;
        }

        public virtual async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context,
            EndpointFilterDelegate next)
        {
            var brand = context.GetArgument<string>(0);
            Logger.LogInformation("{MethodName} called for {brand}", _methodName, brand);
            return await next(context);
        }
    }

    static class ResultsExtensions
    {
        public static IResult Html(this IResultExtensions resultExtensions, string html)
        {
            ArgumentNullException.ThrowIfNull(resultExtensions);

            return new HtmlResult(html);
        }
    }

    class HtmlResult : IResult
    {
        private readonly string _html;

        public HtmlResult(string html)
        {
            _html = html;
        }

        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = MediaTypeNames.Text.Html;
            httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_html);
            return httpContext.Response.WriteAsync(_html);
        }
    }

    static class CSharpEleven
    {
        #region Required members

        public static void RequiredMembers()
        {
            var p1 = new Person { Name = "Shehryar", Surname = "Khan" };
            Person p2 = new("Shehryar", "Khan");

            // Initializations with missing required properties 
            var p3 = new Person { Name = "Shehryar" };
            Person p4 = new();
        }

        public class Person
        {
            public Person() { }

            [SetsRequiredMembers]
            public Person(string name, string surname)
            {
                Name = name;
                Surname = surname;
            }

            public Guid Id { get; set; } = Guid.NewGuid();
            public required string Name { get; set; }
            public required string Surname { get; set; }
        }

        #endregion

        #region Raw string literals

        public static void RawStringLiterals()
        {
            string name = "Shehryar";
            string surname = "Khan";

            // C# 10
            string jsonString10 =
              $@"
              {{
                'Name': {name},
                'Surname': {surname}
              }}
              ";

            // C# 11
            string jsonString11 =
              $$"""
              {
                "Name": {{name}},
                "Surname": {{surname}}
              }
              """;

            var htmlStringLiteral = $"""
                <!doctype html>
                <html>
                    <head><title>Rum</title></head>
                    <body>
                        <h1>Hello Rum World</h1>
                        <p>The time on the server is {DateTime.Now:O}</p>
                    </body>
                </html>
            """;
        }

        #endregion

        #region UTF-8 string literals
        public static void Utf8Literals()
        {
            // C# 10
            byte[] array10 = Encoding.UTF8.GetBytes("Hello World");

            // C# 11
            byte[] array11 = "Im UTF-8"u8.ToArray();
        }
        #endregion

        #region List patterns

        public static void ListPatterns()
        {
            var numbers = new[] { 1, 2, 3, 4 };
            // List and constant patterns 
            Console.WriteLine(numbers is [1, 2, 3, 4]); // True 
            Console.WriteLine(numbers is [1, 2, 4]); // False
            
            // List and discard patterns 
            Console.WriteLine(numbers is [_, 2, _, 4]); // True 
            Console.WriteLine(numbers is [.., 3, _]); // True
            
            // List and logical patterns 
            Console.WriteLine(numbers is [_, >= 2, _, _]); // True
        }

        #endregion

        #region String interpolation

        public static void StringInterpolation()
        {
            int month = 5;
            string season = $"The season is {month switch
            {
                1 or 2 or 12 => "winter",
                > 2 and < 6 => "spring",
                > 5 and < 9 => "summer",
                > 8 and < 12 => "autumn",
            }}.";

            Console.WriteLine(season);
            // The season is spring.

            // LINQ query in string interpolation 
            int[] numbers = new int[] { 1, 2, 3, 4, 5, 6 };
            string message = $"The reversed even values of {nameof(numbers)} are {
                string.Join(", ", numbers.Where(n => n % 2 == 0)
                  .Reverse())}.";

            Console.WriteLine(message);
        }

        #endregion

        #region Static interface members

        public interface IGetNext<T> where T : IGetNext<T>
        {
            static abstract T operator ++(T other);
        }

        public struct RepeatSequence : IGetNext<RepeatSequence>
        {
            private const char Ch = 'A';
            public string Text = new string(Ch, 1);

            public RepeatSequence() { }

            public static RepeatSequence operator ++(RepeatSequence other)
                => other with { Text = other.Text + Ch };

            public override string ToString() => Text;
        }

        public static void StaticInterfaceMembers()
        {
            var str = new RepeatSequence();

            for (int i = 0; i < 10; i++)
                Console.WriteLine(str++);
        }

        #endregion
    }
}