#pragma once
#include <memory>
#include <grpc++/grpc++.h>
#include "malproxy.grpc.pb.h"

typedef malproxy::MalproxyInterface::Stub MalproxyStub;
typedef std::shared_ptr<MalproxyStub> MalproxyStubPtr;

class MalproxyStubContainer
{
public:
	void Set(std::shared_ptr<MalproxyStub> stub);
	void Set(std::shared_ptr<grpc::Channel> channel);
	MalproxyStub* operator->();
	MalproxyStub* get();
private:
	std::shared_ptr<MalproxyStub> _stub;
};
