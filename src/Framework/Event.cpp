#include "Event.h"

#include <chrono>

#ifdef _WIN32
#include <windows.h>
#endif

Event::Event(bool is_auto_reset) :
	_is_auto_reset(is_auto_reset),
	_stop_event_mutex(),
	_stop_event()
{
}


Event::~Event()
{
}


bool Event::Wait(long timeout_ms)
{
	std::unique_lock<std::mutex> stop_event_lock(_stop_event_mutex);
	if (_event_raised)
	{
		if (_is_auto_reset)
			_event_raised = false;

		return true;
	}
	bool retval = _stop_event.wait_for(stop_event_lock, std::chrono::milliseconds(timeout_ms), [this] { return this->_event_raised; });
	if (retval)
	{
		if (_is_auto_reset)
			_event_raised = false;
	}
	return retval;
}

void Event::WaitForever()
{
	std::unique_lock<std::mutex> stop_event_lock(_stop_event_mutex);
	if (_event_raised)
	{
		if (_is_auto_reset)
			_event_raised = false;

		return;
	}
	_stop_event.wait(stop_event_lock);

	if (_is_auto_reset)
		_event_raised = false;
}

void Event::Set()
{
	std::lock_guard<std::mutex> stop_event_lock(_stop_event_mutex);
	_event_raised = true;

	/*
	UGLY but necessary: There is a deadlock, probably found in the implementation of nt.dll on windows 7 32bit (at least). If ExitProcess was called, and therfore _cexit from the CRT, then the notify_all
	will be deadlocked. This is Windows-Specific.
	*/
#ifdef _WIN32
	{
		auto RtlDllShutdownInProgress = reinterpret_cast<BOOLEAN(WINAPI *)()>(GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "RtlDllShutdownInProgress"));

		if (RtlDllShutdownInProgress && RtlDllShutdownInProgress())
			return; // we are during shutdown, notify_all will probably dead-lock us, and the process will exit in a second anyway. 
	}
#endif

	_stop_event.notify_all();
}
