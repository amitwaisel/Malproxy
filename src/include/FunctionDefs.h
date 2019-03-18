#pragma once
#include <vector>
#include <functional>
#include <memory>
#include <vector>
#include "malproxy.pb.h"

struct FunctionCallInterface;

class CalllbackException : public std::runtime_error
{
public:
	CalllbackException(std::string& message) : std::runtime_error(message.c_str()) { }
	CalllbackException(const char* message) : std::runtime_error(message) { }
};

typedef std::string bytearray;

typedef std::function<malproxy::CallFuncResponse(const malproxy::CallFuncRequest&)> CallFuncCallback;
typedef std::function<malproxy::LoadLibraryResponse(const malproxy::LoadLibraryRequest&)> LoadLibraryCallback;
typedef std::function<void(const malproxy::FreeLibraryRequest&)> FreeLibraryCallback;

template<typename Callback>
using CallbackProtectorPtr = std::unique_ptr<Callback, std::function<void(Callback*)>>;
template<typename Callback>
class CallbackProtector
{
public:
	CallbackProtector(Callback* cb_ptr, Callback cb) : _original_cb(*cb_ptr)
	{
		if (cb_ptr != nullptr)
			*cb_ptr = cb;
		_cb = CallbackProtectorPtr<Callback>(cb_ptr, [this](Callback* cb) { if (cb != nullptr) *cb = _original_cb; });
	}
private:
	Callback _original_cb;
	CallbackProtectorPtr<Callback> _cb;
};

