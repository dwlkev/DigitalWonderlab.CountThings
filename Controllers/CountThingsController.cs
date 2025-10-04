using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Core.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;



[ApiController]
[Route("umbraco/api/countthings")]
public class CountThingsController : ControllerBase
{
    private readonly IContentService _contentService;
    private readonly IMediaService _mediaService;
    private readonly IUserService _userService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IFileService _fileService;
    //private readonly ILanguageService _languageService;
    private readonly IDataTypeService _dataTypeService;
    private readonly IMemberService _memberService;
    private readonly IMediaTypeService _mediaTypeService;
    private readonly IMemberTypeService _memberTypeService;
    //private readonly ILocalizationService _localizationService;
    private readonly IScopeProvider _scopeProvider;

    public CountThingsController(
        IContentService contentService,
        IMediaService mediaService,
        IUserService userService,
        IContentTypeService contentTypeService,
        IFileService fileService,
        IMediaTypeService mediaTypeService,
        IMemberTypeService memberTypeService,
        IMemberService memberService,
        IDataTypeService dataTypeService,
        //ILocalizationService localizationService,
        IScopeProvider scopeProvider)
    {
        _contentService = contentService;
        _mediaService = mediaService;
        _userService = userService;
        _contentTypeService = contentTypeService;
        _fileService = fileService;
        _mediaTypeService = mediaTypeService;
        _memberTypeService = memberTypeService;
        _memberService = memberService;
        _dataTypeService = dataTypeService;
        //_localizationService = localizationService;
        _scopeProvider = scopeProvider;
    }

    [HttpGet("content")]
    public IActionResult GetContentCount()
    {
        var allContent = _contentService.GetPagedDescendants(-1, 0, int.MaxValue, out _).ToList();

        var trashedCount = allContent.Count(c => c.Trashed);
        var publishedCount = allContent.Count(c => c.Published && !c.Trashed);
        var unpublishedCount = allContent.Count(c => !c.Published && !c.Trashed);

        int redirectCount;
        using (var scope = _scopeProvider.CreateScope())
        {
            redirectCount = scope.Database.ExecuteScalar<int>("SELECT COUNT(*) FROM umbracoRedirectUrl");
        }

        return Ok(new
        {
            total = allContent.Count, // This includes all items, trashed or not
            published = publishedCount,
            unpublished = unpublishedCount,
            trashed = trashedCount,
            redirects = redirectCount
        });
    }




    [HttpGet("media")]
    public IActionResult GetMediaCount()
    {
        var totalMedia = _mediaService.Count();
        var allMediaItems = _mediaService.GetPagedDescendants(-1, 0, int.MaxValue, out _).ToList();

        var folders = allMediaItems.Count(m => m.ContentType.Alias == "Folder");

        var imageAliases = new HashSet<string> { "Image", "umbracoMediaVectorGraphics" };
        var images = allMediaItems.Count(m => imageAliases.Contains(m.ContentType.Alias));

        var videoAliases = new HashSet<string> { "umbracoMediaVideo", "umbracoMediaAudio" };
        var videos = allMediaItems.Count(m => videoAliases.Contains(m.ContentType.Alias));

        //var largeImages = allMediaItems
        //    .Where(m => imageAliases.Contains(m.ContentType.Alias) && m.GetValue<int>("umbracoBytes") > 2_097_152)
        //    .Count();        

        var largeFiles = allMediaItems
        .Where(m => m.HasProperty("umbracoBytes")
                 && m.GetValue<int>("umbracoBytes") > 2_097_152
                 && m.ContentType.Alias != "Folder")
        .Count();

        // pull unknowns out as "other"
        var otherFilesCount = allMediaItems.Count(m =>
        !imageAliases.Contains(m.ContentType.Alias) &&
        !videoAliases.Contains(m.ContentType.Alias) &&
        m.ContentType.Alias != "Folder");

        // NORMALISE extension: null/empty -> "_unknown", trim dot, lowercase
        var fileTypeCounts = allMediaItems
            .Where(m => m.HasProperty("umbracoExtension"))
            .Select(m => m.GetValue<string>("umbracoExtension"))
            .Select(ext => string.IsNullOrWhiteSpace(ext)
                ? "_unknown"
                : ext.Trim().TrimStart('.').ToLowerInvariant())
            .GroupBy(ext => ext)
            .ToDictionary(g => g.Key, g => g.Count());

        // return all known extensions (including docs/xls/pdf/etc.), exclude the placeholder
        var filteredFileTypeCounts = fileTypeCounts
            .Where(kvp => kvp.Key != "_unknown")
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return Ok(new
        {
            total = totalMedia,
            folders,
            images,
            videos,
            //largeImages,
            largeFiles,
            other = otherFilesCount,
            fileTypes = filteredFileTypeCounts
        });
    }







    [HttpGet("users")]
    public IActionResult GetUserCount()
    {
        var totalUsers = _userService.GetAll(0, int.MaxValue, out _).Count();
        var activeUsers = _userService.GetAll(0, int.MaxValue, out _).Count(u => u.IsApproved);
        var lockedUsers = _userService.GetAll(0, int.MaxValue, out _).Count(u => u.IsLockedOut);

        // Inside your GetUserCount() method, replace user groups block with this:
        int totalUserGroups = 0;

        var spRoot = StaticServiceProvider.Instance;

        // Try to find IUserGroupService in likely namespaces/assemblies (v14 → v16)
        Type userGroupSvcType = Type.GetType("Umbraco.Cms.Core.Services.IUserGroupService, Umbraco.Cms.Core")
                                ?? Type.GetType("Umbraco.Cms.Core.Security.IUserGroupService, Umbraco.Cms.Core")
                                ?? Type.GetType("Umbraco.Cms.Core.Services.UserGroupService, Umbraco.Cms.Core");

        if (userGroupSvcType != null)
        {
            // Use DI scope (scoped service)
            var scopeFactory = spRoot.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
            using var diScope = scopeFactory?.CreateScope();
            var sp = diScope?.ServiceProvider ?? spRoot;

            var userGroupSvc = sp.GetService(userGroupSvcType);
            if (userGroupSvc != null)
            {
                // 1) Try async GetAllAsync
                var getAllAsync = userGroupSvcType.GetMethod("GetAllAsync", new[] { typeof(int), typeof(int) });
                if (getAllAsync != null)
                {
                    try
                    {
                        var task = getAllAsync.Invoke(userGroupSvc, new object[] { 0, int.MaxValue });
                        var taskType = task?.GetType();
                        var resultProp = taskType?.GetProperty("Result");
                        var result = resultProp?.GetValue(task);
                        if (result != null)
                        {
                            var totalProp = result.GetType().GetProperty("Total");
                            if (totalProp != null)
                            {
                                totalUserGroups = Convert.ToInt32(totalProp.GetValue(result) ?? 0);
                                goto DoneUserGroups;
                            }
                            var itemsProp = result.GetType().GetProperty("Items");
                            var items = itemsProp?.GetValue(result) as System.Collections.IEnumerable;
                            if (items != null)
                            {
                                totalUserGroups = items.Cast<object>().Count();
                                goto DoneUserGroups;
                            }
                        }
                    }
                    catch { }
                }

                // 2) Try synchronous GetAll()
                var getAllSync = userGroupSvcType.GetMethod("GetAll", Type.EmptyTypes);
                if (getAllSync != null)
                {
                    try
                    {
                        var groupsObj = getAllSync.Invoke(userGroupSvc, null) as System.Collections.IEnumerable;
                        if (groupsObj != null)
                        {
                            totalUserGroups = groupsObj.Cast<object>().Count();
                            goto DoneUserGroups;
                        }
                    }
                    catch { }
                }

                // 3) Try methods like GetMany, Filter etc
                var getMany = userGroupSvcType.GetMethod("GetMany", new[] { typeof(int), typeof(int), typeof(int).MakeByRefType() });
                if (getMany != null)
                {
                    try
                    {
                        object[] args = { 0, int.MaxValue, 0 };
                        var manyResult = getMany.Invoke(userGroupSvc, args) as System.Collections.IEnumerable;
                        if (manyResult != null)
                        {
                            totalUserGroups = manyResult.Cast<object>().Count();
                            goto DoneUserGroups;
                        }
                    }
                    catch { }
                }
            }
        }

        // Fallback: legacy IUserService.GetAllUserGroups
        {
            var getAllUG = typeof(Umbraco.Cms.Core.Services.IUserService)
                .GetMethod("GetAllUserGroups", new[] { typeof(int[]) });
            if (getAllUG != null)
            {
                try
                {
                    var groups = getAllUG.Invoke(_userService, new object[] { Array.Empty<int>() }) as System.Collections.IEnumerable;
                    if (groups != null)
                        totalUserGroups = groups.Cast<object>().Count();
                }
                catch { }
            }
        }

    // Final fallback: SQL on user group table
    DoneUserGroups:
        if (totalUserGroups == 0)
        {
            try
            {
                using var s = _scopeProvider.CreateScope();
                totalUserGroups = s.Database.ExecuteScalar<int>("SELECT COUNT(*) FROM umbracoUserGroup");
                s.Complete();
            }
            catch { }
        }

        // Then your `userGroups = totalUserGroups` in the JSON

        // ---------------------------------------------------------------------------


        var totalMembers = _memberService.GetAll(0, int.MaxValue, out _).Count();
        var totalMemberGroups = _memberService.GetAllRoles().Count();

        return Ok(new
        {
            total = totalUsers,
            active = activeUsers,
            locked = lockedUsers,
            userGroups = totalUserGroups,   // now works on v14/15/16
            members = totalMembers,
            memberGroups = totalMemberGroups
        });
    }


    [HttpGet("schema")]
    public IActionResult GetSchemaCount()
    {
        var totalDocTypes = _contentTypeService.GetAll().Count();
        var totalTemplates = _fileService.GetTemplates().Count();
        var totalPartials = _fileService.GetPartialViews().Count();
        var totalScripts = _fileService.GetScripts().Count();
        var totalStylesheets = _fileService.GetStylesheets().Count();
        var totalMediaTypes = _mediaTypeService.GetAll().Count();
        var totalMemberTypes = _memberTypeService.GetAll().Count();
        var totalDataTypes = _dataTypeService.GetAll().Count();

        // ---- v13–v16 compatible language count (no compile-time types) ----
        int totalLanguages = 0;
        var sp = StaticServiceProvider.Instance;

        // Try v14+ first: ILanguageService.GetAll()
        var langServiceType = Type.GetType("Umbraco.Cms.Core.Services.ILanguageService, Umbraco.Cms.Core");
        if (langServiceType != null)
        {
            var langService = sp.GetService(langServiceType);
            var getAll = langServiceType.GetMethod("GetAll", Type.EmptyTypes);
            if (langService != null && getAll != null)
            {
                var langs = getAll.Invoke(langService, null) as System.Collections.IEnumerable;
                if (langs != null) totalLanguages = langs.Cast<object>().Count();
            }
        }
        else
        {
            // Fallback to v13: ILocalizationService.GetAllLanguages()
            var locServiceType = Type.GetType("Umbraco.Cms.Core.Services.ILocalizationService, Umbraco.Cms.Core");
            if (locServiceType != null)
            {
                var locService = sp.GetService(locServiceType);
                var getAllLangs = locServiceType.GetMethod("GetAllLanguages", Type.EmptyTypes);
                if (locService != null && getAllLangs != null)
                {
                    var langs = getAllLangs.Invoke(locService, null) as System.Collections.IEnumerable;
                    if (langs != null) totalLanguages = langs.Cast<object>().Count();
                }
            }
        }
        // -------------------------------------------------------------------

        var totalSchema = totalDocTypes + totalTemplates + totalPartials + totalScripts +
                          totalStylesheets + totalMediaTypes + totalMemberTypes +
                          totalDataTypes + totalLanguages;

        return Ok(new
        {
            total = totalSchema,
            doctypes = totalDocTypes,
            templates = totalTemplates,
            partials = totalPartials,
            scripts = totalScripts,
            stylesheets = totalStylesheets,
            mediatypes = totalMediaTypes,
            membertypes = totalMemberTypes,
            datatypes = totalDataTypes,
            languages = totalLanguages
        });
    }




    [HttpGet("forms")]
    public IActionResult GetFormsCount()
    {
        // A) Is Forms installed? (assembly check only)
        var hasFormsAssembly = AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(a => a.GetName().Name.Equals("Umbraco.Forms.Core", StringComparison.OrdinalIgnoreCase));

        if (!hasFormsAssembly)
            return Ok(new { installed = false, total = 0, entries = 0 });

        // B) Types (no compile-time refs)
        var formRepoType = Type.GetType("Umbraco.Forms.Core.Persistence.Repositories.IFormRepository, Umbraco.Forms.Core");
        var recordReaderType = Type.GetType("Umbraco.Forms.Core.Services.IRecordReaderService, Umbraco.Forms.Core");

        // C) Create an Umbraco DB scope AND a DI scope (critical: repo is scoped)
        using var umbScope = _scopeProvider.CreateScope(autoComplete: true);

        var root = StaticServiceProvider.Instance;
        var scopeFactory = root.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
        using var diScope = scopeFactory?.CreateScope();
        var sp = diScope?.ServiceProvider ?? root; // fall back to root if needed

        object formRepo = formRepoType != null ? sp.GetService(formRepoType) : null;
        object recordReader = recordReaderType != null ? sp.GetService(recordReaderType) : null;

        int formCount = 0;
        long entryCount = 0;

        // D) Try repository first (reflection)
        if (formRepo != null)
        {
            var getMany = formRepoType.GetMethod("GetMany", Type.EmptyTypes);
            var formsObj = getMany?.Invoke(formRepo, null) as System.Collections.IEnumerable;
            var forms = formsObj?.Cast<object>().ToList() ?? new List<object>();
            formCount = forms.Count;

            // Sum entries with IRecordReaderService if available (still paged)
            if (recordReader != null)
            {
                var readerMethod = recordReaderType.GetMethod(
                    "GetRecordsFromForm",
                    new[] { typeof(Guid), typeof(int), typeof(int) });

                foreach (var formEntity in forms)
                {
                    if (formEntity is null) continue;

                    // v14+ uses 'Key'; keep some aliases just in case
                    var keyProp = formEntity.GetType().GetProperty("Key")
                              ?? formEntity.GetType().GetProperty("Id")
                              ?? formEntity.GetType().GetProperty("UniqueId");

                    if (keyProp == null) continue;
                    if (keyProp.GetValue(formEntity) is Guid key && readerMethod != null)
                    {
                        try
                        {
                            var pageObj = readerMethod.Invoke(recordReader, new object[] { key, 1, 1 });
                            var totalItemsProp = pageObj?.GetType().GetProperty("TotalItems");
                            if (totalItemsProp != null)
                                entryCount += Convert.ToInt64(totalItemsProp.GetValue(pageObj) ?? 0L);
                        }
                        catch
                        {
                            // ignore; we'll fall back to SQL below
                        }
                    }
                }
            }
        }

        // E) DB fallbacks (version-proof)
        //    - Number of forms
        if (formCount == 0)
        {
            try { formCount = Convert.ToInt32(umbScope.Database.ExecuteScalar<long>("SELECT COUNT(*) FROM UFForms")); }
            catch { /* leave 0 */ }
        }

        //    - Total entries across all forms
        if (entryCount == 0 && formCount > 0)
        {
            try { entryCount = umbScope.Database.ExecuteScalar<long>("SELECT COUNT(*) FROM UFRecords"); }
            catch { /* leave 0 */ }
        }

        return Ok(new { installed = true, total = formCount, entries = entryCount });
    }





}
