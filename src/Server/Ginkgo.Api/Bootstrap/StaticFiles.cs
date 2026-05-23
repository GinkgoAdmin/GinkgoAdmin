using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace Ginkgo.Api.Bootstrap;

public static class StaticFiles
{
    /// <summary>
    /// Map static files for uploads and resource directories with extension whitelist.
    /// Mirrors the logic previously in Program.cs to keep behavior unchanged.
    /// </summary>

    /// <summary>
    /// Normalize a path for the current OS. On Linux, if the path looks like a Windows
    /// absolute path (contains backslash or drive letter), fall back to a safe default
    /// under ContentRootPath.
    /// </summary>
    private static string NormalizePath(string raw, string contentRootPath, string fallbackSubDir)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return Path.Combine(contentRootPath, fallbackSubDir);

        // Detect Windows-style path on Linux (e.g. "D:\project\..." or contains backslash)
        if (OperatingSystem.IsLinux() && (raw.Contains('\\') || (raw.Length >= 2 && raw[1] == ':')))
        {
            Console.WriteLine($"[StaticFiles] Windows path detected on Linux: {raw}, falling back to {contentRootPath}/{fallbackSubDir}");
            return Path.Combine(contentRootPath, fallbackSubDir);
        }

        if (!Path.IsPathRooted(raw))
            return Path.GetFullPath(Path.Combine(contentRootPath, raw));

        return raw;
    }

    public static void UseStaticFileMappings(this WebApplication app, IConfiguration configuration, string uploadsRoot, bool installationMode)
    {
        if (installationMode) return; // keep exact previous behavior

        // Request path for uploads
        var staticRequestPath = configuration["Upload:RequestPath"] ?? "/uploads";

        // Normalize uploadsRoot for current OS
        uploadsRoot = NormalizePath(uploadsRoot, app.Environment.ContentRootPath, "uploads");
        Console.WriteLine($"[StaticFiles] Resolved uploadsRoot: {uploadsRoot}");

        // Ensure uploads directory exists (create if missing; fallback to temp if creation fails)
        if (!Directory.Exists(uploadsRoot))
        {
            try
            {
                Directory.CreateDirectory(uploadsRoot);
                Console.WriteLine($"Created uploads directory: {uploadsRoot}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create uploads directory: {uploadsRoot}, Error: {ex.Message}");
                // Use temp directory as fallback
                uploadsRoot = Path.Combine(Path.GetTempPath(), "GinkgoUploads");
                Directory.CreateDirectory(uploadsRoot);
                Console.WriteLine($"Using temporary uploads directory: {uploadsRoot}");
            }
        }

        // Map /uploads -> uploadsRoot
        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = staticRequestPath,
            FileProvider = new PhysicalFileProvider(uploadsRoot)
        });

        // Map /resource -> configured physical directory with extension whitelist
        try
        {
            var resourcePhysical = configuration["Resource:PhysicalPath"];
            var resourceRequest = configuration["Resource:RequestPath"] ?? "/resource";

            // Cross-platform fix: normalize resource path
            if (!string.IsNullOrWhiteSpace(resourcePhysical))
            {
                resourcePhysical = NormalizePath(resourcePhysical, app.Environment.ContentRootPath, "resource");
                Console.WriteLine($"[StaticFiles] Resolved resourcePhysical: {resourcePhysical}");
            }

            if (string.IsNullOrWhiteSpace(resourcePhysical))
            {
                // Fallback 1: <contentRoot>/resource (typical for published deployments)
                var fallback1 = Path.Combine(app.Environment.ContentRootPath, "resource");
                // Fallback 2: Dev layout <repoRoot>/resource relative to content root
                var fallback2 = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "..", "resource"));
                if (Directory.Exists(fallback1))
                    resourcePhysical = fallback1;
                else if (Directory.Exists(fallback2))
                    resourcePhysical = fallback2;
            }

            if (!string.IsNullOrWhiteSpace(resourcePhysical) && Directory.Exists(resourcePhysical))
            {
                // Default allowed extensions (overridable via Resource:AllowedExtensions)
                var defaultAllowed = new[]
                {
                    ".jpg",".jpeg",".png",".gif",".webp",".svg",
                    ".css",".js",".map",".html",".htm",
                    ".woff",".woff2",".ttf",".eot",".otf",
                    ".mp4",".webm",".ico",".txt",".pdf"
                };
                var allowedExtensions = configuration.GetSection("Resource:AllowedExtensions").Get<string[]>() ?? defaultAllowed;
                var allowedSet = new HashSet<string>(allowedExtensions, StringComparer.OrdinalIgnoreCase);

                app.UseStaticFiles(new StaticFileOptions
                {
                    RequestPath = resourceRequest,
                    FileProvider = new PhysicalFileProvider(resourcePhysical),
                    ServeUnknownFileTypes = false,
                    ContentTypeProvider = new FileExtensionContentTypeProvider(),
                    OnPrepareResponse = ctx =>
                    {
                        try
                        {
                            var path = ctx.File.PhysicalPath;
                            var ext = Path.GetExtension(path);
                            if (string.IsNullOrEmpty(ext) || !allowedSet.Contains(ext))
                            {
                                ctx.Context.Response.StatusCode = 404;
                                ctx.Context.Response.ContentLength = 0;
                                ctx.Context.Response.Body.SetLength(0);
                            }
                        }
                        catch { }
                    }
                });

                Console.WriteLine($"Static resource mounted: {resourceRequest} => {resourcePhysical}");
            }
            else
            {
                Console.WriteLine("Static resource mount skipped: Resource:PhysicalPath not set or directory not found.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to mount static resource directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Host a Vue/React SPA from wwwroot directory. Serves static files and falls back
    /// to index.html for client-side routing (any non-API, non-file request).
    /// Place web/dist/* contents into wwwroot/ under the application root.
    /// </summary>
    public static bool UseSpaStaticFiles(this WebApplication app)
    {
        var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        if (!Directory.Exists(wwwroot))
        {
            Console.WriteLine($"[SPA] wwwroot directory not found at: {wwwroot}, SPA hosting skipped.");
            return false;
        }

        var indexHtml = Path.Combine(wwwroot, "index.html");
        if (!File.Exists(indexHtml))
        {
            Console.WriteLine($"[SPA] index.html not found in wwwroot, SPA hosting skipped.");
            return false;
        }

        Console.WriteLine($"[SPA] Hosting SPA from: {wwwroot}");

        var fileProvider = new PhysicalFileProvider(wwwroot);

        // Serve default files (index.html) for root path
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = fileProvider
        });

        // Serve static files from wwwroot (js, css, images, etc.)
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = fileProvider,
            ServeUnknownFileTypes = false
        });

        Console.WriteLine("[SPA] Static file middleware registered.");
        return true;
    }

    /// <summary>
    /// Host UniApp H5 build from h5/ directory under ContentRootPath.
    /// Serves static files at /h5 and falls back to /h5/index.html for client-side routing.
    /// </summary>
    public static bool UseH5StaticFiles(this WebApplication app)
    {
        var contentRoot = app.Environment.ContentRootPath;
        Console.WriteLine($"[H5] ContentRootPath: {contentRoot}");
        Console.WriteLine($"[H5] BaseDirectory: {AppContext.BaseDirectory}");

        // 尝试多个候选路径查找 h5 目录
        var candidates = new[]
        {
            Path.Combine(contentRoot, "h5"),
            Path.Combine(AppContext.BaseDirectory, "h5")
        };

        string? h5Root = null;
        foreach (var candidate in candidates)
        {
            Console.WriteLine($"[H5] Checking: {candidate} (exists: {Directory.Exists(candidate)})");
            if (Directory.Exists(candidate))
            {
                h5Root = candidate;
                break;
            }
        }

        if (h5Root == null)
        {
            Console.WriteLine("[H5] h5 directory not found at any candidate path, H5 hosting skipped.");
            return false;
        }

        var indexHtml = Path.Combine(h5Root, "index.html");
        if (!File.Exists(indexHtml))
        {
            Console.WriteLine($"[H5] index.html not found in {h5Root}, H5 hosting skipped.");
            return false;
        }

        // 列出 h5 目录内容用于诊断
        try
        {
            var entries = Directory.GetFileSystemEntries(h5Root);
            Console.WriteLine($"[H5] Directory contents ({entries.Length} items):");
            foreach (var e in entries)
                Console.WriteLine($"[H5]   {Path.GetFileName(e)}");
            var assetsDir = Path.Combine(h5Root, "assets");
            if (Directory.Exists(assetsDir))
            {
                var assetFiles = Directory.GetFiles(assetsDir);
                Console.WriteLine($"[H5] assets/ contains {assetFiles.Length} files:");
                foreach (var f in assetFiles.Take(10))
                    Console.WriteLine($"[H5]   {Path.GetFileName(f)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[H5] Error listing directory: {ex.Message}");
        }

        Console.WriteLine($"[H5] Hosting UniApp H5 from: {h5Root}");

        var fileProvider = new PhysicalFileProvider(h5Root);

        // Serve default files (index.html) when accessing /h5/
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            RequestPath = "/h5",
            FileProvider = fileProvider
        });

        // Serve static files from h5/ at /h5 path
        app.UseStaticFiles(new StaticFileOptions
        {
            RequestPath = "/h5",
            FileProvider = fileProvider,
            ServeUnknownFileTypes = false,
            ContentTypeProvider = new FileExtensionContentTypeProvider()
        });

        Console.WriteLine("[H5] Static file middleware registered at /h5.");
        return true;
    }

    /// <summary>
    /// Register H5 SPA fallback: any unmatched /h5/** request serves h5/index.html.
    /// Must be called AFTER app.MapControllers() so API routes take priority.
    /// </summary>
    public static void MapH5Fallback(this WebApplication app)
    {
        var h5Root = Path.Combine(app.Environment.ContentRootPath, "h5");
        var indexHtml = Path.Combine(h5Root, "index.html");
        if (!File.Exists(indexHtml)) return;

        app.MapFallback("/h5/{**path}", async context =>
        {
            // Only serve index.html for navigation requests (not static assets).
            // If the request path has a file extension (e.g. .js, .css, .png),
            // it means the static file middleware already tried and failed to find it,
            // so return 404 instead of serving index.html with wrong MIME type.
            var requestPath = context.Request.Path.Value ?? "";
            var ext = Path.GetExtension(requestPath);
            if (!string.IsNullOrEmpty(ext))
            {
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(indexHtml);
        }).AllowAnonymous(); // 关键：H5 SPA 入口必须匿名放行，否则全局 FallbackPolicy 会把浏览器导航请求拦成 401。

        Console.WriteLine("[H5] Fallback route /h5/{**path} -> h5/index.html registered.");
    }

    /// <summary>
    /// Register SPA fallback route: any unmatched GET request serves wwwroot/index.html.
    /// Must be called AFTER app.MapControllers() so API routes take priority.
    /// </summary>
    public static void MapSpaFallback(this WebApplication app)
    {
        var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var indexHtml = Path.Combine(wwwroot, "index.html");
        if (!File.Exists(indexHtml)) return;


        app.MapFallback("/config/{**path}", async context =>
        {
            context.Response.StatusCode = 404;
            context.Response.ContentLength = 0;
        }).AllowAnonymous();

        var fileProvider = new PhysicalFileProvider(wwwroot);

        // MapFallbackToFile serves index.html for any request not matched by
        // controllers, static files, or other endpoints — exactly what SPA needs.
        // 关键：必须 AllowAnonymous，否则全局 FallbackPolicy = RequireAuthenticatedUser 会把
        // 浏览器对 /admin、/web、/login 等 SPA 路径的导航请求直接 401，前端连 index.html 都拿不到。
        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            FileProvider = fileProvider
        }).AllowAnonymous();

        Console.WriteLine("[SPA] Fallback route to index.html registered.");
    }
}

