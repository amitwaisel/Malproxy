#include "MalproxyClient.h"
#include <chrono>

#include "../Framework/Utils.h"

using namespace std::chrono_literals;

MalproxyClient::MalproxyClient(std::shared_ptr<MalproxyStubContainer> malproxy_stub) :
    stub(malproxy_stub)
{
}

MalproxyClient::~MalproxyClient()
{
	try
	{
	}
	catch (...) {}
}

MalproxyClient::ClientContextGuard MalproxyClient::AddContext(ClientContextPtr& context)
{
	{
		std::lock_guard<std::mutex> lock(client_contexts_lock);
		client_contexts.push_back(context);
	}

	ClientContextGuard clientContext_guard(&context, [this](ClientContextPtr* ctx)
	{
		RemoveContext(ctx);
	});
	return clientContext_guard;
}

void MalproxyClient::RemoveContext(ClientContextPtr* context)
{
	if (context == nullptr) return;
	std::lock_guard<std::mutex> lock(client_contexts_lock);
	client_contexts.remove(*context); // If the equality comparison between elements is guaranteed to not throw, the function never throws exceptions (no-throw guarantee)
}

void MalproxyClient::CancelClientContexts()
{
	ClientContexts local_contexts;
	{
		std::lock_guard<std::mutex> lock(client_contexts_lock);
		local_contexts = ClientContexts(client_contexts);
		client_contexts.clear();
	}
	for (auto context : local_contexts)
	{
		context->TryCancel();
	}
}

malproxy::CallFuncResponse MalproxyClient::CallFunc(const malproxy::CallFuncRequest& request)
{
	ClientContextPtr client_context = std::make_shared<grpc::ClientContext>();
	auto context_guard = AddContext(client_context); // locks grpc_lock reader

	malproxy::CallFuncResponse response;
	auto status = stub->get()->CallFunc(client_context.get(), request, &response);
	if (!status.ok())
		THROW("Failed calling remote func");
	return response;
}

malproxy::LoadLibraryResponse MalproxyClient::LoadRemoteLibrary(const malproxy::LoadLibraryRequest& request)
{
	ClientContextPtr client_context = std::make_shared<grpc::ClientContext>();
	auto context_guard = AddContext(client_context); // locks grpc_lock reader

	malproxy::LoadLibraryResponse response;
	auto status = stub->get()->LoadRemoteLibrary(client_context.get(), request, &response);
	if (!status.ok())
		THROW("Failed calling remote load library");
	return response;
}

void MalproxyClient::FreeRemoteLibrary(const malproxy::FreeLibraryRequest& request)
{
	ClientContextPtr client_context = std::make_shared<grpc::ClientContext>();
	auto context_guard = AddContext(client_context); // locks grpc_lock reader

	malproxy::Empty response;
	auto status = stub->get()->FreeRemoteLibrary(client_context.get(), request, &response);
	if (!status.ok())
		THROW("Failed calling remote load library");
}

