using Platform.Authoring.Application;
using Platform.Authoring.Domain;

namespace Platform.Api.Modules.Authoring;

//Development-only storage for DraftService
//All stored data is lost when the application stops & register is a singleton so drafts survive across HTTP requests
public sealed class DevelopmentDraftRepository : IDraftRepository
{
    private readonly Dictionary<Guid, LabDraft> _drafts = new();
    private readonly object _sync = new();

    //GetAsync loads the draft
    public Task<LabDraft?> GetAsync(Guid id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock(_sync)
        {
            LabDraft? result = _drafts.TryGetValue(id, out var draft) ? Copy(draft) : null;
            return Task.FromResult(result);
        }
    }

    //AddAsync saves the new draft
    public Task AddAsync(LabDraft draft, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock(_sync)
        {
            //Checking and inserting under the same lock prevents concurrent requests from reuisng an existing draft ID
            if(_drafts.ContainsKey(draft.Id))
            {
                throw new DomainRuleException(AuthoringErrorCodes.Conflict);
            }

            _drafts.Add(draft.Id, Copy(draft));
        }

        return Task.CompletedTask;
    }

    //SaveAsync saves new changes in a draft 
    public Task SaveAsync(LabDraft draft, int previousVersion, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock(_sync)
        {
            if(!_drafts.TryGetValue(draft.Id, out var stored))
            {
                throw new DomainRuleException(AuthoringErrorCodes.NotFound);
            }

            //Preserve IDraftRepository's optimistic concurrency contract - an old version must never overwrite a newer change
            if(stored.Version != previousVersion)
            {
                throw new DomainRuleException(AuthoringErrorCodes.Conflict);
            }

            _drafts[draft.Id] = Copy(draft);
        }

        return Task.CompletedTask;
    }

    //Return a separate instance of a draft so changes must go through SaveAsync
    private static LabDraft Copy(LabDraft draft) => LabDraft.Rehydrate(draft.Id, draft.OwnerId, draft.SourceTemplateId, draft.Status, draft.Content, draft.Version);
}