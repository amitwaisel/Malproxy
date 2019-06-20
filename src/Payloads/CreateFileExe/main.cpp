#include <Windows.h>
#include <stdio.h>

int main(int argc, char* argv[])
{
	HANDLE x = CreateFileW(L"C:\\temp\\blah.txt", GENERIC_READ, 0, NULL, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL);

	wchar_t buff[1024] = {0};
	swprintf(buff, sizeof(buff), L"Created file with handle %p. Error code %d. argc is %d", x, GetLastError(), argc);
	OutputDebugStringW(buff);
	for (int i = 0; i < argc; i++)
		OutputDebugStringA(argv[i]);
}