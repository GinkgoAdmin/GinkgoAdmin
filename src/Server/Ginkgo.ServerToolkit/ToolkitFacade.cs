namespace Ginkgo.ServerToolkit;

internal sealed class ServerToolkitFacade : IServerToolkit
{
	public ServerToolkitFacade(ICurrentUser currentUser, IServerNotifier notifier, IEmailSender emailSender, IVerificationCodeService verificationCodes, ISecondaryVerificationService secondFactor)
	{
		CurrentUser = currentUser; Notifier = notifier; EmailSender = emailSender; VerificationCodes = verificationCodes; SecondFactor = secondFactor;
	}

	public ICurrentUser CurrentUser { get; }
	public IServerNotifier Notifier { get; }
	public IEmailSender EmailSender { get; }
	public IVerificationCodeService VerificationCodes { get; }
	public ISecondaryVerificationService SecondFactor { get; }
}







