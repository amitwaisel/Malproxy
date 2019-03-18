#pragma once

#include <mutex>
#include <condition_variable>

class Event
{
public:
	Event(bool is_auto_reset = true);
	virtual ~Event();

	void WaitForever();
	// Returns true if event raised, false if timeout has expired
	bool Wait(long timeout_ms);
	// Signals quit event
	void Set();

private:
	std::mutex _stop_event_mutex;
	std::condition_variable _stop_event;
	bool _event_raised = false;
	bool _is_auto_reset;
};
