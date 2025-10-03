using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Core.DependencyInjection; // for StaticServiceProvider
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

        // -------- v14/15/16 compatible user-group count --------
        int totalUserGroups = 0;

        // Try v16+ service first
        var userGroupServiceType = Type.GetType("Umbraco.Cms.Core.Services.IUserGroupService, Umbraco.Cms.Core");
        if (userGroupServiceType != null)
        {
            // Resolve via the root provider (works inside an Umbraco scope too)
            var sp = Umbraco.Cms.Core.DependencyInjection.StaticServiceProvider.Instance;
            var userGroupService = sp.GetService(userGroupServiceType);

            // Most builds expose GetAll(): IEnumerable<IUserGroup>
            var getAll = userGroupServiceType.GetMethod("GetAll", Type.EmptyTypes);
            if (userGroupService != null && getAll != null)
            {
                var groupsObj = getAll.Invoke(userGroupService, null) as System.Collections.IEnumerable;
                if (groupsObj != null) totalUserGroups = groupsObj.Cast<object>().Count();
            }
            else
            {
                // Fallback in case the API is slightly different; try "GetMany(int,int,out int)"
                var getMany = userGroupServiceType.GetMethod("GetMany",
                    new[] { typeof(int), typeof(int), typeof(int).MakeByRefType() });
                if (userGroupService != null && getMany != null)
                {
                    object[] args = { 0, int.MaxValue, 0 };
                    var many = getMany.Invoke(userGroupService, args) as System.Collections.IEnumerable;
                    if (many != null) totalUserGroups = many.Cast<object>().Count();
                }
            }
        }
        else
        {
            // v14/v15 path: call the old IUserService method via reflection (so v16 still compiles)
            var getAllGroupsOld = typeof(Umbraco.Cms.Core.Services.IUserService)
                .GetMethod("GetAllUserGroups", Type.EmptyTypes);
            if (getAllGroupsOld != null)
            {
                var groupsObj = getAllGroupsOld.Invoke(_userService, null) as System.Collections.IEnumerable;
                if (groupsObj != null) totalUserGroups = groupsObj.Cast<object>().Count();
            }
        }
        // -------------------------------------------------------

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
        // 1) Detect Umbraco Forms assembly
        var hasForms = AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(a => a.GetName().Name.Equals("Umbraco.Forms.Core", StringComparison.OrdinalIgnoreCase));

        if (!hasForms)
        {
            return Ok(new { installed = false, total = 0, entries = 0 });
        }

        // 2) Resolve types by name (no compile-time reference)
        var formRepoType = Type.GetType("Umbraco.Forms.Core.Persistence.Repositories.IFormRepository, Umbraco.Forms.Core");
        var recordReaderType = Type.GetType("Umbraco.Forms.Core.Services.IRecordReaderService, Umbraco.Forms.Core");
        if (formRepoType is null || recordReaderType is null)
        {
            // Forms present but API types not found (unexpected / version mismatch)
            return Ok(new { installed = false, total = 0, entries = 0 });
        }

        using var scope = _scopeProvider.CreateScope(autoComplete: true);

        // Use the root provider. Repositories will still run inside the ambient scope created above.
        var sp = StaticServiceProvider.Instance;

        var formRepo = sp.GetService(formRepoType);
        var recordReader = sp.GetService(recordReaderType);
        if (formRepo is null || recordReader is null)
        {
            return Ok(new { installed = false, total = 0, entries = 0 });
        }

        // 3) Call IFormRepository.GetMany()
        var getMany = formRepoType.GetMethod("GetMany", Type.EmptyTypes);
        if (getMany is null)
            return Ok(new { installed = false, total = 0, entries = 0 });

        var formsObj = getMany.Invoke(formRepo, null);
        var formsEnum = (formsObj as System.Collections.IEnumerable) ?? Array.Empty<object>();

        int formCount = 0;
        long entryCount = 0;

        // 4) For each FormEntity, read Guid Key and call IRecordReaderService.GetRecordsFromForm(Guid, int, int)
        var readerMethod = recordReaderType.GetMethod(
            "GetRecordsFromForm",
            new[] { typeof(Guid), typeof(int), typeof(int) });

        foreach (var formEntity in formsEnum)
        {
            if (formEntity is null) continue;
            formCount++;

            var keyProp = formEntity.GetType().GetProperty("Key"); // Guid Key on FormEntity in v14
            if (keyProp == null) continue;

            var keyVal = keyProp.GetValue(formEntity);
            if (keyVal is Guid key && readerMethod != null)
            {
                var pageObj = readerMethod.Invoke(recordReader, new object[] { key, 1, 1 });
                if (pageObj != null)
                {
                    var totalItemsProp = pageObj.GetType().GetProperty("TotalItems");
                    if (totalItemsProp != null)
                    {
                        var totalItems = Convert.ToInt64(totalItemsProp.GetValue(pageObj) ?? 0L);
                        entryCount += totalItems; // long to avoid overflow
                    }
                }
            }
        }

        return Ok(new { installed = true, total = formCount, entries = entryCount });
    }


}
