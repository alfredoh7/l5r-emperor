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
        Emit("attachmentTransferred", $"{reason}: {Definition(attachment).Name} was transferred from {Definition(from).Name} to {Definition(host).Name}.", host.OwnerId, attachment.Id);
    }
}
