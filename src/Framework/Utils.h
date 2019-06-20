#pragma once
#include <exception>
#include <string>
#include <cstdlib>
#include <memory>
#include <cstdarg>

#define THROW(msg, ...) throw std::runtime_error(StringUtils::FormatString(msg, __VA_ARGS__).c_str())

namespace StringUtils
{
	std::string FormatString(const char* fmt_str, ...);
	std::wstring FormatString(const wchar_t* fmt_str, ...);
	std::wstring Utf8ToUtf16(const std::string& utf8_str);
	std::string Utf16ToUtf8(const std::wstring& utf16_str);
	
	std::wstring Basename(const std::wstring& path);
}
