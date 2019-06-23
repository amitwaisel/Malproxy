#pragma once
#include <memory>
#include <list>
#include <functional>
#include <grpc++/grpc++.h>

#include "malproxy.grpc.pb.h"

#include "RpcCallbacks.h"
#include "MalproxyStub.h"

class RpcException : public std::runtime_error
{
public:
	RpcException(std::string& message) : std::runtime_error(message.c_str()) { }
	RpcException(const char* message) : std::runtime_error(message) { }
};

class MalproxyClient {
public:
	MalproxyClient(std::shared_ptr<MalproxyStubContainer> rsc_stub);
	virtual ~MalproxyClient();

	MalproxyClient(const MalproxyClient& other) = delete;
	MalproxyClient& operator=(const MalproxyClient& other) = delete;

	malproxy::CallFuncResponse CallFunc(const malproxy::CallFuncRequest& request);
	malproxy::LoadLibraryResponse LoadRemoteLibrary(const malproxy::LoadLibraryRequest& request);
	malproxy::LoadLibraryResponse LoadRemoteLibraryEx(const malproxy::LoadLibraryExRequest& request);
	void FreeRemoteLibrary(const malproxy::FreeLibraryRequest& request);

private:

	typedef std::shared_ptr<grpc::ClientContext> ClientContextPtr;
	typedef std::list<ClientContextPtr> ClientContexts;
	typedef ClientContexts::const_iterator ClientContextsIterator;
	typedef std::function<void(ClientContextPtr*)> ClientContextRemover;
	typedef std::shared_ptr<ClientContextPtr> ClientContextGuard;

	ClientContextGuard AddContext(ClientContextPtr& context);
	void RemoveContext(ClientContextPtr* context);
	void CancelClientContexts();

private:
	grpc::CompletionQueue _completion_queue;
    ClientContexts client_contexts;
    std::mutex client_contexts_lock;
    grpc::ClientContext collectors_context;
    std::shared_ptr<MalproxyStubContainer> stub;
};

