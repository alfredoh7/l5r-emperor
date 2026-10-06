namespace L5R.Core;

// Transfer (core rules Glossary): "move an attachment or token from one Personality/province to another; no need to re-pay its cost, but attaching
// restrictions still apply." Seeds of Decay pass (issue #453, batch 4): Daigotsu Yamato ("Transfer it to Yamato"), Hiruma Sawai ("Transfer a Follower from
// one to the other") and Cavalry Detachment ("Transfer the target's attachments to the created Personality"). The attachment keeps its state (bowed,
// tokens, modifiers) and only changes host: no cost is paid, no entering-play window opens (it never left play), and the grants an attachment gives its
// wielder (AttachGrantsKeyword) follow it to the new host. The three cards move an attachment between two Personalities of the SAME controller, so a
// transfer never changes who controls it; a transfer across controllers is a change of control (docs/decisions/2026-09-12-unique-singular-control-transfer.md)
// and is not built here.
public sealed partial class MatchEngine
{
    // The attaching restrictions a transfer must still satisfy: the destination is a Personality in play of the same controller, not the current host, the
    // attachment's own "will only/not attach to" rule accepts it, and the Armor/Weapon limits hold. Unique is not re-checked: the card is already the one
    // copy its controller controls.
    private bool CanTransferTo(CardInstance attachment, CardInstance host) =>
        attachment.Zone == Zone.Attached && attachment.AttachedTo != host.Id && host.OwnerId == attachment.OwnerId && !WillNotBeTransferred(attachment)
        && InPlay(host) && Definition(host).Type == CardType.Personality
        && AttachmentLegalTarget(Definition(attachment), host) && AttachmentAllowed(attachment, host);

    private void TransferAttachment(CardInstance attachment, CardInstance host, string reason)
    {
        Require(CanTransferTo(attachment, host), $"{Definition(attachment).Name} cannot be transferred to {Definition(host).Name}.");
        var from = Card(attachment.AttachedTo);
        RemoveAttachmentGrants(from, attachment.Id);
        attachment.AttachedTo = host.Id; attachment.ProvinceIndex = host.ProvinceIndex;
        if (Definition(attachment).AttachGrantsKeyword is string granted) GrantKeyword(host, granted, attachment.Id, GrantDuration.Attachment);
        SearchPlayTransferGrants(attachment, host); // The Topaz Armor - exp (issue #667): the two keywords chosen for it follow it
        Emit("attachmentTransferred", $"{reason}: {Definition(attachment).Name} was transferred from {Definition(from).Name} to {Definition(host).Name}.", host.OwnerId, attachment.Id);
    }

    // "Transfer this card to your target Personality" (Field Messenger, Communion): the attaching restrictions are part of what makes the target legal, so they
    // are checked as the action is announced, not only as it resolves. Checked late, an illegal target was only refused after a before-action Reaction window
    // had closed, which made the other player's decline the rejected command (issue #672: the bot soak stalled on exactly that).
    private void ValidateSelfTransfer(string id, GameCommand command, CardInstance? source)
    {
        if (id is not ("field-messenger-transfer" or "communion-transfer") || source is null || command.TargetId is not { } targetId) return;
        var host = Card(targetId);
        Require(CanTransferTo(source, host), $"{Definition(source).Name} cannot be transferred to {Definition(host).Name}.");
    }

    // Registration only: callbacks are not invoked during module initialization.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255", Justification = "Register feature callbacks before any engine or catalogue reader.")]
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void RegisterAttachmentTransferHooks() =>
        EngineHooks.ValidateLate.Register("ValidateSelfTransfer", 13100, static (engine, context) => engine.ValidateSelfTransfer(context.id, context.command, context.source));
}
