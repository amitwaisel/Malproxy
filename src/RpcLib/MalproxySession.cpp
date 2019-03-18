#include "MalproxySession.h"

MalproxySession::MalproxySession() : _stub(std::make_shared<MalproxyStubContainer>())
{ }

void MalproxySession::Connect(std::string url, std::shared_ptr<grpc::ChannelCredentials> channel_creds)
{
	client = std::make_shared<MalproxyClient>(_stub);

	grpc::ChannelArguments channel_arguments;
	channel_arguments.SetInt(GRPC_ARG_MAX_MESSAGE_LENGTH, 20971520); // 20MB
	channel_arguments.SetInt(GRPC_ARG_INITIAL_RECONNECT_BACKOFF_MS, 60*1000); // 1 minute
	channel_arguments.SetInt(GRPC_ARG_MAX_RECONNECT_BACKOFF_MS, 10*1000); // 10 seconds
	
	_stub->Set(grpc::CreateCustomChannel(url, channel_creds, channel_arguments));

	started = true; // Mark started here to allow Disconnect() to cancel the connection if necessary
}

MalproxySession::~MalproxySession()
{
	try
	{
		Disconnect();
	}
	catch (...) {}
}

malproxy::CallFuncResponse MalproxySession::CallFunc(const malproxy::CallFuncRequest& request) const
{
	return client->CallFunc(request);
}

malproxy::LoadLibraryResponse MalproxySession::LoadRemoteLibrary(const malproxy::LoadLibraryRequest& request) const
{
	return client->LoadRemoteLibrary(request);
}

void MalproxySession::FreeRemoteLibrary(const malproxy::FreeLibraryRequest& request) const
{
	client->FreeRemoteLibrary(request);
}

void MalproxySession::Disconnect()
{
	if (!started)
		return;
	started = false;
	_stub.reset();
	client.reset();
}

bool MalproxySession::IsConnected() const
{
	return started;
}
