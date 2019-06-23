#pragma once

#include <grpc++/grpc++.h>

#include "MalproxyClient.h"
#include "RpcCallbacks.h"

class MalproxySession
{
public:
	MalproxySession();
	virtual ~MalproxySession(); // Calls Disconnect()

	void Connect(std::string url, std::shared_ptr<grpc::ChannelCredentials> channel_creds);
	void Disconnect();
	bool IsConnected() const;
	
	malproxy::CallFuncResponse CallFunc(const malproxy::CallFuncRequest& request) const;
	malproxy::LoadLibraryResponse LoadRemoteLibrary(const malproxy::LoadLibraryRequest& request) const;
	malproxy::LoadLibraryResponse LoadRemoteLibraryEx(const malproxy::LoadLibraryExRequest& request) const;
	void FreeRemoteLibrary(const malproxy::FreeLibraryRequest& request) const;

private:
	bool started = false;
	std::shared_ptr<MalproxyStubContainer> _stub;
	std::shared_ptr<MalproxyClient> client;
};

