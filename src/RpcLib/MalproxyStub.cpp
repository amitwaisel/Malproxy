#include "MalproxyStub.h"

void MalproxyStubContainer::Set(std::shared_ptr<MalproxyStub> stub)
{
	_stub = stub;
}

void MalproxyStubContainer::Set(std::shared_ptr<grpc::Channel> channel)
{
	Set(malproxy::MalproxyInterface::NewStub(channel));
}

MalproxyStub* MalproxyStubContainer::operator->()
{
	return get();
}
MalproxyStub* MalproxyStubContainer::get()
{
	return _stub.get();
}
