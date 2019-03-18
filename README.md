# Malproxy
Proxy system calls over an RPC channel

## Abstract

During a classic cyber attack, one of the major offensive goals is to execute code remotely on valuable machines. The purpose of that code varies on the spectrum from information extraction to physical damage. As defenders, our goal is to detect and eliminate any malicious code activity, while hackers continuously find ways to bypass the most advanced detection mechanisms. It’s an endless cat-and-mouse game where new mitigations and features are continuously added to the endpoint protection solutions and even the operating system (OS) itself, to protect the users against newly discovered attack techniques.

Abstract In this paper, we present a new approach for malicious code to bypass most of endpoint protection measures. Our approach covertly proxies the malicious code operations over the network, never deploying the actual malicious code on the victim side. We are going to execute code on an endpoint, without really storing the code on disk or loading it to memory. This technique potentially allows attackers to run malicious code on remote victims, in such a way that the code is undetected by the victim’s security solutions. We denote this technique as “malproxying”.

## Malproxy Solution

Our goal is to implement and show a proof of concept for allowing malicious code execution on an endpoint, without really storing the code on disk or loading it to memory.

Our approach can be implemented on various operating systems. We chose to focus on Windows environment as this is the most popular operating system, both for users and malware, and it is by far the most attacked operating system that exists today.

As a proof of concept, we make some assumptions about the environment. We assume the attacker has remote code execution capabilities on the victim, with highest SYSTEM privileges that allow the execution of our victim-side component. We assume all operations of that component may be monitored by endpoint security solution — as to be discussed in the “Bypassing Endpoint Security” section below.

An application executed on some machine runs in the context of that environment. It relies on the output returned from external library functions.

The logical flow of any application is predetermined on compilation time (or on evaluation time for scripts), thus controlling the output returned from the system calls will control and change the way the code is executed. This fundamental fact is the base for any sandboxing tool or virtualization platform.

An application that does not rely on any external library function is basically a piece of code executed by the CPU, that runs the same flow of code on any platform. On the other hand, when an application relies on the operating system calls and reacts to the environment and ecosystem in which it is executed, the execution flow may differ among different machines and platforms.

The foundations of our solution lay on the emulation/virtualization of system calls mechanism that exists on most popular operating systems. We created a tool that has two main components:

1. One runs on the attacker machine, and we will refer to it as the attacker side of the tool; its purpose is to emulate any kind of Windows PE file (DLL or EXE) and control all the system calls that are called from inside the sandboxed file.
2. The other runs on the victim machine, and we will refer to it as the victim side of our solution; its purpose is to receive system call requests, execute those and send the response back to the attacker side.

Both components communicate over some network protocol that allows the attacker side to send system call requests to the victim side. As mentioned above, those system calls can be either Win32 API functions or Native API functions. We will dive deeper into both possibilities later on.

By proxying all imported system calls from the attacker side to the victim component, the emulated code “at home” executes the same code flow as it would have been executed if it was launched directly on the victim side.

Proxying Windows system calls is far from being trivial. Win32 API functions have several possible prototypes, which we describe below. We partially covered those types in our proof of concept. Future research and work may cover the rest to allow proxying of any Win32 API call.

### Proxying Win32 API Functions

Our solution has to deal with all aspects of different prototypes when it proxies any Win32 API or any Native API function:

1. The attacker side has to simulate the same calling convention as the original API function, so the emulated malicious code will behave as expected.
2. The attacker side has to serialize all input arguments — primitives and pointers, so they will be parsed and analyzed by the victim side.
3. The victim side has to serialize all output arguments. The attacker side has to deserialize them and set the original output arguments accordingly.
4. The return value must be serialized on the victim side and parsed on the attacker side. Dealing with the return value may be quite complicated, as explained below.

We have to pay attention to some possible pitfalls:

1. Input/output buffer arguments may contain pointers to other internal structures. The serialization and parsing logic can be recursively applied to proxy those internal objects.
2. In our proof of concept we implemented a default shallow serialization so internal pointers are serialized/parsed by-value. This means that one sidethe one-side may encounter a pointer to some memory address which is valid on the other sideother-side.
3. Our implementation supports adding custom code for serialization and parse of every structure — so all internal pointers will be proxied correctly. For example, when a linked-list node is passed, it might be required to serialize and deserialize the entire list - because the malicious code iterates over it. Therefore, the malicious logic dictates how those structures have to be handled.

#### Handling Input Arguments

Input arguments passed to the proxied functions are treated as follows:

1. Primitives are sent to the victim side by value, and passed to the system call also by-value.
2. Pointers to primitives (that are input arguments) are serialized with the value stored in the pointed memory block. The value is then deserialized by the victim side, placed into a new allocated memory block, and passed to the system call as pointer.
3. User-allocated buffers are serialized and sent to the victim side. On arrival, the victim side allocates the required memory block, places the deserialized buffer in it, and passes its address to the system call.

#### Handling Output Arguments

Output primitives are always represented as pointers to some memory block. The victim side has to read the content of those blocks and send it to the attacker side, which puts the contents in the original pointers passed to the proxied function.

For example, if the function has a DWORD* output argument arg, the solution runs as follows:

1. If arg is also an input argument, its contents are serialized and sent to the victim side.
2. The victim side allocates a block of memory (on the stack or on the heap) and initializes it with the value sent from the attacker side. The address of this block is then passed to the system call.
3. Once the system call returns, the new contents of the memory block are serialized and sent to the attacker side.
4. The attacker side deserialized the value and puts it in the original “arg” pointer.

Handling with output buffers has to be done carefully to cover all possible cases: User-allocated buffers and system-allocated buffers.

##### Handling User-Allocated Output Buffers

A user-allocated output buffer is passed as a pointer on the attacker side. The attacker component sends this buffer (with its contents if the argument is also input-directed) to the victim side. There, the victim component allocates a new buffer and initializes it with the received data. The address of that victim-side allocated buffer is passed to the system call. Once the system call returns, the contents of the buffer are serialized and sent back to the attacker side. Then, the attacker deserializes the transferred contents to the original user-allocated buffer. The victim component can now free the buffer allocated on its side.

##### Handling System-Allocated Output Buffers

A system-allocated output buffer is more complex to handle. The user passes a pointer where it expects the system call to put the address of the system-allocated buffer. This address will be passed in a later stage to some dedicated deallocation function. Therefore, until the deallocation function is called, that address has to be valid.

In our case, the emulated code on the attacker side passes some pointer. The attacker component has nothing to serialize, so it calls the victim side with some unique identifier (“tag”). On the victim side, our component will call the system function with some other pointer. That pointer will be initialized by the system call with the address of the victim-side-buffer. The victim component will have to serialize the contents of that buffer, and send it to the attacker side. There, the attacker component will allocate a new attacker-side-buffer, where it will place the contents received from the victim. The address of that buffer will be stored in the user-supplied pointer.

Both the attacker and victim sides have to store references for every system-allocated buffer, and link it to the unique tag sent for every request. Those buffers must not be freed automatically. The coupling between the attacker-side-buffer and the victim-side-buffer can be done using the previously-mentioned unique tag. Once the emulated code on the attacker side calls the deallocation function on the attacker-allocated-buffer, our platform will have to:

1. Extract the unique tag related to that attacker-side-buffer.
2. Proxy the call to the victim side with that unique tag, where the relevant victim-side-buffer is cached. The victim component will extract the victim-side-buffer from cache, based on its tagging. Then, the deallocation function will be called with the victim-side-buffer address.
3. Free the attacker-side-buffer itself from the heap.

As mentioned above, handling complex output structures with internal pointers requires custom implementation of the serialization and parse logic of those structures. Otherwise, the default shallow-copy implementation will be used, passing memory pointers from the victim side to the attacker side, while those addresses are most probably invalid on the attacker side.

#### Handling Return Values and Exception

All return values have to be serialized on the victim side and sent back to the attacker side, where those are parsed and returned to the caller. Primitive return values are simply transferred and returns by their value. On the other hand, non-primitive return values have to be treated as buffers. On the attacker side, our proxying logic has to parse it into an allocated memory block, and return the address of that memory block. Our implementation has to keep track of those blocks if they require custom deallocation. If the original APIs return value should not be freed, our implementation will have to leak it — so the memory address will remain valid after the proxy stub is done.

If a structured exception is raised on the victim-side stub, it is handled and reported to the attacker-side stub. The attacker stub will “rethrow” the exception by calling RaiseException API with the relevant arguments received from the victim side.

#### Handling Asynchronous Overlapped API

When an overlapped operation is initiated by passing an OVERLAPPED structure, the buffer passed to the system call has to remain valid until GetOverlappedResult returns true value. Therefore, both attacker and victim stubs must be aware of the overlapped operation. A user-allocated buffer passed to the function has to be cached both on the attacker and victim stubs. The GetOverlappedResult API has to be proxied as well, and handle serialization and deserialization of every buffer returned by using the aforementioned cached buffer. Only after completing the overlapped operation, the cached buffer can be freed from the cache on both sides.


### Bypassing Endpoint Protection

Our goal as an adversary is to run our malicious code without being detected by any security solution installed on the victim side. As we now know, security products have various techniques to detect, monitor and block any known and unknown piece of malicious code. Therefore, our implementation aims to evade those detection measures, by using the techniques described below. As the victim-side component is the only piece of code that potentially runs on a security-controlled environment, our goal is to implement evasive measures that will bypass most of the known security solutions monitoring capabilities.

As explained, the role of the victim-side component is to receive system call requests over the network, perform the actual system call, and send the results back to the attacker-side component. Those results can be either the return value or the out-directed parameters. To keep the victim side as “innocent” as possible, we implement it to be lightweight, and hide its true nature — both statically and dynamically (for behavioral analysis).

We tackle all types of detection mechanism that most modern security solutions have. For each detection technique, we describe our bypass. This bypass may be effective for some endpoint protection products, and others might still detect it. Thorough implementation and checks can make the tool hidden from most endpoint solutions.

#### Bypassing Static Signatures

As we know, authors of static signatures wish to avoid false positives. Therefore, they try to find a piece of code or data that is unique to the analyzed malicious code. To use this fact to our advantage, we used as many third-party open-source libraries as we could, instead of implementing ad hoc code on our own (and as a nice bonus, it significantly reduced the coding time). We don’t use any known bad third-party library.

In our implementation, the server-side is the victim. Therefore, it doesn’t initiate the command-and-control connection to the attacker, so the code doesn’t contain any IP addresses or domain names. In addition, the victim-side component is lightweight and does not contain any attack logic — lists of files, registry keys, process names and so on.

#### Bypassing Static-Heuristic Signatures

We wish to reduce the “heuristic fingerprint” of our victim-side code. Therefore, we try to avoid putting suspicious strings, encryption algorithms implementations or high-entropy data blocks inside our binary.

Moreover, we import some USER32.dll API calls, which interact with the user and reduced the heuristic fingerprint. On the other hand, our component does not import “malicious” API functions, as those are loaded dynamically by calling LoadLibrary/GetProcAddress functions. Therefore, those APIs cannot be located by static analysis tools.

In addition, we did not encrypt or obfuscate our binary in any way, as we know security solutions suspect those techniques as malicious.

#### Bypassing Sandboxing-Based Detection

Our tool can detect sandbox execution scenario by using some publicly known techniques. With the awareness regarding possible sandbox execution, our victim-side component can call many “noisy” and “user related” API functions, which will be executed only under a sandbox context. This will cause the sandbox to check those fake code flows, and not the hidden real flow.

Because the victim-side code will not perform any system call requested over the proxy, no malicious operations will be done when a sandbox environment is detected.

#### Bypassing Dynamic Signatures

Dynamic analysis is more complicated to bypass or fool, as it tracks the system calls executed by our process. When some malicious activity requires a set of system calls to be executed in some order, there is no way around it — the code must execute those system calls. Nonetheless, we do have some tricks in our sleeve to make the security solution’s life much harder.

Our first trick is to “break” the execution flow of the malicious code, so endpoint security solutions will have a very hard time to keep track on what system calls are executed. Having a hard time will most probably increase the amount of potential false positive detections — and will make those signatures unusable by the protection solutions.

For example, solutions that keep track on the execution flow of every thread usually apply their signatures on a single thread execution log — as looking on all threads may result many false-positive detections. So, our component can execute every system call on a separate thread. It does increase the resource overhead of the application, but it is most probably neglectable compared to the need of “fooling” the endpoint protection monitoring logs.

Another trick is to treat the “problem” at its source — and hide the execution of the system calls from the monitoring process. The endpoint protection product might either monitor the system calls at user-mode level, or at kernel-mode level.

If the security product monitors the system calls on user-mode level, its detection capabilities are quite limited. Those capabilities are based on user-mode hooks, hot-patches or callback registration provided by the operating system. For each of those techniques, there are ways adversaries can hide themselves:

1. Hooks and hot-patches exist on the virtual address space of some system DLLs. Loading another copy of those DLLs, from disk, using some reflective-DLL-loading technique, will load a fresh copy of that library, without the presence of any hook. Therefore, calling the exported function of the reflective library, rather than the original library, will not “go through” the detection mechanism of the security product.
2. Callback registration is a service provided by the operating system to notify its registered users when some event is triggered in the system. Sometimes, those notifications can be easily avoided by calling lower-level DLL rather than the formal one. For example, it is possible to call NTDLL functions directly, avoiding the code in KERNEL32 or KERNELBASE libraries.

If the security product monitors the system calls on kernel-mode level — our victim-side code has to have a kernel component as well. This malicious kernel driver will allow the user to avoid using NTDLL NtXxx functions, which simply make the system call to their ZwXxx matching functions. Instead, sending the system call request to our driver, over some IOCTL operation, will result the driver to call the kernel’s ZwXxx export directly, or even to use more internal kernel functions. In many cases, filter/monitoring drivers installed by security solutions turn a blind eye to any operation that is originated from kernel code.

Moreover, our driver can simply attach itself to any user-mode process context, and run its actions in that process context, That process can be chosen wisely so the system call will appear legitimate when it is executed at that process context.

Using the technique of user-mode code execution from the kernel will fool the dynamic monitoring even more, making it almost impossible to keep track on the behavioral flow of the malicious code.

#### Reputation-Based and Hypervisor-Based Signatures

As mentioned above, those signature types are out of scope for this paper and our proof of concept. The victim-side component would have to tackle those monitoring and detection mechanisms when needed.

## Implementation

We implemented our proof of concept on Windows environment, using Google’s gRPC as our RPC-based communication platform. The attacker-side application receives a PE file (either a DLL or EXE), loads them to memory and starts their execution. For EXE files, the entry point is called, while for DLL files we look for an exported function names “Run” and call it.

The precompilation process we implemented parses a configuration file, containing all the properties of the functions we wish to proxy: names, return values and parameters. That process generates two C++ source files - one for the attacker side (“home”) and one for the victim side (“field”). Those files are automatically deployed in the MalproxyClient and MalproxyServer projects, and compiled together with those components.

The automatically generated code provides basic support for various function prototypes. It is supported to manually customize each stub to allow ad hoc implementation for each stub.

Loading the PE files to memory is done using the open-source library MemoryModulehttps://github.com/fancycode/MemoryModule. This library allows us to use custom LoadLibrary, GetProcAddress and FreeLibrary implementations, instead of the original functions. Those custom functions are used during the loading process, when the loader resolves the addresses of the imported functions. For each imported function, the loader loads the relevant DLL that exports that function, resolves the address of the function and places it in the import table.

In the Malproxy case, we wish to proxy some of the functions to the field side, while allowing local execution of the non-proxied functions. For the proxied functions, our custom LoadLibrary sends a request to the victim side to load the relevant library. Then, the custom GetProcAddress returns the address of the relevant proxy stub on the attacker side. For other functions, their real addresses are returned.

Each attacker-side proxy stub uses the globally-available RPC session to initiate an RPC request handled by the victim side. The victim-side stub is responsible to parse the input arguments, get the address of the original function, call it with all the relevant parsed arguments (while handling the output-directed arguments if exist), and then serialize both the returned value and the out-arguments. The values are returned to the attacker side where they are parsed and placed in the original addresses of the output arguments. The returned value is also parsed and returned from the attacker-side stub.

The following code snippets show how the attacker-side and victim-side stubs are designed:

Attacker-side automatically-generated stub:
```C++
DWORD WINAPI Malproxy_NtQuerySystemInformation(
	MalproxySession* client
	, DWORD SystemInformationClass
	, LPVOID* SystemInformation, DWORD SystemInformationLength
	, DWORD* ReturnLength
) {
	malproxy::CallFuncRequest request;
	request.set_dll_name("ntdll.dll");
	request.set_function_name("NtQuerySystemInformation");

	malproxy::Argument* arg_SystemInformationClass = request.add_in_arguments();
	arg_SystemInformationClass->set_uint32_val((DWORD)SystemInformationClass);
	malproxy::Argument* arg_SystemInformation = request.add_in_arguments();
	malproxy::Argument* arg_ReturnLength = request.add_in_arguments();

	auto response = client->CallFunc(request);
	auto out_buffer_SystemInformation = response.out_arguments(0).buffer_val();
	auto out_buffer_SystemInformation_data = out_buffer_SystemInformation.data();
	if (out_buffer_SystemInformation.type() == malproxy::BufferType::BufferType_UserAllocated && !out_buffer_SystemInformation_data.empty())
		memcpy(SystemInformation, out_buffer_SystemInformation_data.data(), std::min((DWORD)out_buffer_SystemInformation_data.size(), SystemInformationLength));

	*ReturnLength = (DWORD)response.out_arguments(1).uint32_val();
	return (DWORD)response.return_value().uint32_val();
}
```

Victim-side automatically-generated stub:
```C++
malproxy::CallFuncResponse NtQuerySystemInformation_stub(const malproxy::CallFuncRequest& request, FARPROC real_func)
{
	DWORD SystemInformationClass = (DWORD)request.in_arguments(0).uint32_val();
	LPVOID SystemInformation = nullptr;
	DWORD SystemInformationLength = 0;
	DWORD ReturnLength_val = { 0 }; DWORD* ReturnLength = &ReturnLength_val;
	DWORD raw_retval = ((DWORD(*)(DWORD, LPVOID*, DWORD, DWORD*))real_func)(SystemInformationClass, &SystemInformation, SystemInformationLength, ReturnLength);

	malproxy::CallFuncResponse result;
	malproxy::Argument retval_value; malproxy::Argument* retval = &retval_value;
	retval->set_uint32_val((DWORD)raw_retval);
	std::unique_ptr<malproxy::Argument> retval_allocated_ptr = std::make_unique<malproxy::Argument>(retval_value);
	result.set_allocated_return_value(retval_allocated_ptr.release());
	malproxy::Argument* out_SystemInformation = result.add_out_arguments();
	std::unique_ptr<malproxy::BufferArgument> SystemInformation_buffer_ptr = std::make_unique<malproxy::BufferArgument>();
	std::string out_SystemInformation_data; out_SystemInformation_data.assign((char*)SystemInformation, SystemInformationLength);
	SystemInformation_buffer_ptr->set_data(out_SystemInformation_data);
	SystemInformation_buffer_ptr->set_size(SystemInformationLength);
	SystemInformation_buffer_ptr->set_type(malproxy::BufferType::BufferType_UserAllocated);
	out_SystemInformation->set_allocated_buffer_val(SystemInformation_buffer_ptr.release());

	malproxy::Argument* out_ReturnLength = result.add_out_arguments();
	out_ReturnLength->set_uint32_val(*ReturnLength);

	return result;
}
```

We should pay attention that our proxied binary may have many imported functions. Some are fully self-contained in the process code, therefore it is not required to proxy them. We should proxy only the system call functions that interact with the operating system.

For example, every compiled C/C++ binary that uses the CRT library, imports many functions from KERNEL32.dll. Those functions may be called during CRT initialization or deinitialization flows. Some are relevant for proxying and some are not.

Our code is publicly available [here](https://github.com/amitwaisel/Malproxy)

## Interesting Use Cases

We chose some use cases on which we demonstrate the capabilities of our malproxying solution. Each use case is based on widely used hacking tools, which adversaries may upload and execute on the victims machines. By using our malproxying technique, they will be able to proxy the malicious activity and avoid storing those files on the victim side.

The tools we chose to proxy are netcat, sysinternal’s psexec and mimikatz. Proxying each tool requires handling some challenges as we describe below.

### Netcat Proxying

Netcat tool for Windows uses Winsock library to create sockets for connecting or listening on certain ports over TCP or UDP. Thus, it is required to proxy socket-related API. As socket is merely a handle to system resources, it can be handled the same way like any other handle type. For our demonstration purposes, we focus on synchronous socket operations.

[TBD]

### PsExec Proxying

PsExec is a light-weight tool that remotely executes processes on other systems, without having to manually install client software. PsExec's most powerful uses include launching interactive command-prompts on remote systems and remote-enabling tools like IpConfig that otherwise do not have the ability to show information about remote systems.

Note: Some antivirus scanners report that one or more of the tools are infected with a "remote admin" virus. None of the PsTools contain viruses, but they have been used by viruses, which is why they trigger virus notifications.

We would like to allow running psexec from a victim machine, allow it to execute code of another victim, without deploying the suspicious file on the first victim.

[TBD]

### Mimikatz Proxying

Mimikatz is an open-source tool, developed by Benjamin Delpi, that is capable of extracting plaintexts passwords, hashes, PIN codes and kerberos tickets from memory. Mimikatz can also perform pass-the-hash, pass-the-ticket or build Golden tickets. That’s why its techniques and implementation are widely used by adversaries that wish to put their hands on valuable strong Windows accounts.

[TBD]

## Summary

[TBD]

## License
[GNU LGPLv3](https://choosealicense.com/licenses/lgpl-3.0/)