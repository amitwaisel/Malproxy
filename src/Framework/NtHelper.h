#pragma once
#include <Windows.h>
typedef LONG NTSTATUS;
typedef NTSTATUS *PNTSTATUS;

#define STATUS_SUCCESS (NTSTATUS)0x00000000L
#define STATUS_BUFFER_OVERFLOW           ((NTSTATUS)0x80000005L)
#define STATUS_NO_MORE_FILES             ((NTSTATUS)0x80000006L)
#define STATUS_BUFFER_TOO_SMALL          ((NTSTATUS)0xC0000023L)

#define NT_SUCCESS(Status) ((NTSTATUS)(Status) >= 0)
#define NT_ERROR(Status) ((NTSTATUS)(Status) < 0)

typedef struct _UNICODE_STRING {
	USHORT Length;
	USHORT MaximumLength;
	PWSTR  Buffer;
} UNICODE_STRING;

typedef UNICODE_STRING *PUNICODE_STRING;
typedef const UNICODE_STRING *PCUNICODE_STRING;

typedef struct _STRING {
	USHORT Length;
	USHORT MaximumLength;
	PCHAR Buffer;
} STRING;

typedef STRING *PSTRING;
typedef STRING ANSI_STRING;
typedef PSTRING PANSI_STRING;

typedef struct _CURDIR
{
	UNICODE_STRING DosPath;
	PVOID Handle;
} CURDIR, *PCURDIR;

typedef struct _RTL_DRIVE_LETTER_CURDIR
{
	WORD Flags;
	WORD Length;
	ULONG TimeStamp;
	STRING DosPath;
} RTL_DRIVE_LETTER_CURDIR, *PRTL_DRIVE_LETTER_CURDIR;

typedef struct _RTL_USER_PROCESS_PARAMETERS
{
	ULONG MaximumLength;	// 0
	ULONG Length;			// 4
	ULONG Flags;			// 8
	ULONG DebugFlags;		// 12
	PVOID ConsoleHandle;	// 16
	ULONG ConsoleFlags;		// 20
	PVOID StandardInput;	// 24
	PVOID StandardOutput;	// 28
	PVOID StandardError;	// 32
	CURDIR CurrentDirectory;	// 36
	UNICODE_STRING DllPath;		// 48
	UNICODE_STRING ImagePathName;	// 56
	UNICODE_STRING CommandLine;		// 64
	PVOID Environment;
	ULONG StartingX;
	ULONG StartingY;
	ULONG CountX;
	ULONG CountY;
	ULONG CountCharsX;
	ULONG CountCharsY;
	ULONG FillAttribute;
	ULONG WindowFlags;
	ULONG ShowWindowFlags;
	UNICODE_STRING WindowTitle;
	UNICODE_STRING DesktopInfo;
	UNICODE_STRING ShellInfo;
	UNICODE_STRING RuntimeData;
	RTL_DRIVE_LETTER_CURDIR CurrentDirectores[32];
	ULONG EnvironmentSize;
} RTL_USER_PROCESS_PARAMETERS, *PRTL_USER_PROCESS_PARAMETERS;

typedef struct _PEB
{
	BYTE Reserved1[2];
	BYTE BeingDebugged;
	BYTE Reserved2[1];
	PVOID Reserved3[2];
	struct PEB_LDR_DATA* Ldr;
	PRTL_USER_PROCESS_PARAMETERS ProcessParameters;
	BYTE Reserved4[104];
	PVOID Reserved5[52];
	struct PS_POST_PROCESS_INIT_ROUTINE* PostProcessInitRoutine;
	BYTE Reserved6[128];
	PVOID Reserved7[1];
	ULONG SessionId;
} PEB, *PPEB;

typedef struct _PROCESS_BASIC_INFORMATION {
	PVOID Reserved1;
	PPEB PebBaseAddress;
	PVOID Reserved2[2];
	ULONG_PTR UniqueProcessId;
	PVOID Reserved3;
} PROCESS_BASIC_INFORMATION;

typedef enum _PROCESSINFOCLASS {
	ProcessBasicInformation = 0,
	ProcessDebugPort = 7,
	ProcessWow64Information = 26,
	ProcessImageFileName = 27,
	ProcessBreakOnTermination = 29,
	ProcessSubsystemInformation = 75
} PROCESSINFOCLASS, *PPROCESSINFOCLASS;


static NTSTATUS(WINAPI * pRtlInitUnicodeString)(PUNICODE_STRING, PCWSTR) = nullptr;
static BOOLEAN(WINAPI * pRtlCreateUnicodeString)(PUNICODE_STRING, PCWSTR) = nullptr;
static VOID(NTAPI * pRtlFreeUnicodeString)(PUNICODE_STRING UnicodeString) = nullptr;
static VOID(NTAPI * pRtlCopyUnicodeString)(PUNICODE_STRING DestinationString, PCUNICODE_STRING SourceString) = nullptr;
static NTSTATUS(WINAPI * pNtQueryInformationProcess)(HANDLE, PROCESSINFOCLASS, PVOID, ULONG, PULONG) = nullptr;
static ULONG(NTAPI* pRtlNtStatusToDosError)(NTSTATUS Status) = nullptr;

static bool native_functions_initialized = false;
static bool initialize_native_functions(VOID)
{
	if (native_functions_initialized)
		return true;

	HMODULE hModule = GetModuleHandleA("ntdll.dll");
	if (hModule == nullptr)
		return false;

	pRtlInitUnicodeString = (NTSTATUS(WINAPI *)(PUNICODE_STRING, PCWSTR)) GetProcAddress(hModule, "RtlInitUnicodeString");
	pRtlCreateUnicodeString = (BOOLEAN(NTAPI *)(PUNICODE_STRING, PCWSTR)) GetProcAddress(hModule, "RtlCreateUnicodeString");
	pRtlFreeUnicodeString = (VOID(NTAPI *)(PUNICODE_STRING)) GetProcAddress(hModule, "RtlFreeUnicodeString");
	pRtlCopyUnicodeString = (VOID(NTAPI *)(PUNICODE_STRING, PCUNICODE_STRING)) GetProcAddress(hModule, "RtlCopyUnicodeString");
	pNtQueryInformationProcess = (NTSTATUS(WINAPI *)(HANDLE, PROCESSINFOCLASS, PVOID, ULONG, PULONG))GetProcAddress(hModule, "NtQueryInformationProcess");
	pRtlNtStatusToDosError = (ULONG(NTAPI *)(NTSTATUS Status))GetProcAddress(hModule, "RtlNtStatusToDosError");
	native_functions_initialized =
		pRtlInitUnicodeString != nullptr &&
		pRtlCreateUnicodeString != nullptr &&
		pRtlCopyUnicodeString != nullptr &&
		pRtlFreeUnicodeString != nullptr &&
		pNtQueryInformationProcess != nullptr &&
		pRtlNtStatusToDosError != nullptr;
	return native_functions_initialized;
}
