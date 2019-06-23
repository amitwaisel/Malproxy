#include "MalproxyServer.h"

#include <grpc/grpc.h>
#include <grpc++/server.h>
#include <grpc++/server_builder.h>
#include <grpc++/server_context.h>
#include <grpc++/security/server_credentials.h>

#include "../Framework/Utils.h"
#include "../Framework/Event.h"

MalproxyServer::MalproxyServer(const RpcServerCallbacks& callbacks) : _callbacks(callbacks), _server_done(false), _shutdown_done(false)
{
	bool initialized_successfully = false;
	Event ready;

	_server_thread = std::thread([this, &ready, &initialized_successfully]()
	{
		_state = STARTING;
		try
		{
			grpc::ServerBuilder builder;
			builder.AddListeningPort("0.0.0.0:8888", grpc::InsecureServerCredentials());
			builder.RegisterService(this);
			builder.SetOption(grpc::MakeChannelArgumentOption("grpc.so_reuseport", 1));
			builder.AddChannelArgument("grpc.keepalive_time_ms", 100000);
			builder.AddChannelArgument("grpc.keepalive_timeout_ms", 30000);
			builder.AddChannelArgument("grpc.keepalive_permit_without_calls", 1);
			builder.AddChannelArgument("grpc.http2.min_time_between_pings_ms", 5000);
			builder.AddChannelArgument("grpc.http2.min_ping_interval_without_data_ms", 5000);
			builder.AddChannelArgument("grpc.http2.max_ping_strikes", 999999);

			_server = std::move(builder.BuildAndStart());
			if (!_server)
				THROW("Could not create server");

			initialized_successfully = true;
			ready.Set();
		}
		catch (...)
		{
			ready.Set();
			return;
		}
	});

	ready.WaitForever();

	if (!initialized_successfully)
		THROW("Failed initializing RSC server");

	_state = STARTED;
}

MalproxyServer::~MalproxyServer()
{
	try
	{
		Shutdown();
		_shutdown_done.WaitForever();
	}
	catch (...)
	{
		
	}
}


Tag::Tag(std::function<void()> completion_routine, std::function<void()> error_routine)
	: _completion_routine(completion_routine), _error_routine(error_routine)
{

}

void * Tag::getTagId()
{
	return (void*)this;
}

void Tag::Go()
{
	_completion_routine();
}

void Tag::Error()
{
	if (_error_routine != nullptr)
		_error_routine();
}

grpc::Status MalproxyServer::CallFunc(grpc::ServerContext* context, const malproxy::CallFuncRequest* request, malproxy::CallFuncResponse* response)
{
	*response = _callbacks.CallCollectorFunc(*request);
	return grpc::Status();
}

grpc::Status MalproxyServer::LoadRemoteLibrary(grpc::ServerContext * context, const malproxy::LoadLibraryRequest * request, malproxy::LoadLibraryResponse * response)
{
	*response = _callbacks.LoadLibraryFunc(*request);
	return grpc::Status();
}

grpc::Status MalproxyServer::LoadRemoteLibraryEx(grpc::ServerContext * context, const malproxy::LoadLibraryExRequest * request, malproxy::LoadLibraryResponse * response)
{
	*response = _callbacks.LoadLibraryExFunc(*request);
	return grpc::Status();
}

grpc::Status MalproxyServer::FreeRemoteLibrary(grpc::ServerContext * context, const malproxy::FreeLibraryRequest * request, malproxy::Empty * response)
{
	_callbacks.FreeLibraryFunc(*request);
	*response = malproxy::Empty();
	return grpc::Status();
}

void MalproxyServer::Shutdown()
{
	if (_state == STARTING || _state == STARTED)
	{
		_state = SHUTTING_DOWN;

		if (_server != nullptr)
		{
			gpr_timespec timeout = { 0 }; // cancel outstanding RPC calls immediatly, no grace period
			timeout.clock_type = GPR_TIMESPAN;
			_server->Shutdown<gpr_timespec>(timeout);
		}

		_server_thread.join(); //must wait for the completion queue to drain


		_state = SHUTDOWN;
		// When Shutdown() is called from ctrl+c handler, _server_done event is raised and the MalproxyServer's destructor is called.
		// The d'tor calls Shutdown again (while the previous one still runs). This will cause the destructor to finish
		// before the first Shutdown() is done, causing abort() to be called
		_shutdown_done.Set();
	}
}

void MalproxyServer::Wait()
{
	_server->Wait();
	_server_done.Set();
}
