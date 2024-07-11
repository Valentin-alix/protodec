protodec
========
A tool to decompile protobuf classes compiled by [protoc](https://github.com/protocolbuffers/protobuf), from CIL assemblies back into .proto definitions.

Usage
-----
```
Usage: protodec(.exe) <target_assembly_path> <out_path> [options]
Arguments:
  target_assembly_path  Either the path to the target assembly or a directory of assemblies, all of which be parsed.
  out_path              An existing directory to output into individual files, otherwise output to a single file.
Options:
  --debug                                                     Drops the minimum log level to Debug.
  --parse_service_servers                                     Parses gRPC service definitions from server classes.
  --parse_service_clients                                     Parses gRPC service definitions from client classes.
  --skip_enums                                                Skip parsing enums and replace references to them with int32.
  --include_properties_without_non_user_code_attribute        Includes properties that aren't decorated with `DebuggerNonUserCode` when parsing.
  --include_service_methods_without_generated_code_attribute  Includes methods that aren't decorated with `GeneratedCode("grpc_csharp_plugin")` when parsing gRPC services.
```

Limitations
-----------
- Integers are assumed to be (u)int32/64 as CIL doesn't differentiate between them and sint32/64 and (s)fixed32/64.
- Package names are not preserved in protobuf compilation so naturally we cannot recover them during decompilation, which may result in naming conflicts.
- When decompiling from [Il2CppDumper](https://github.com/Perfare/Il2CppDumper) DummyDLLs
    - The `Name` parameter of `OriginalNameAttribute` is not dumped. In this case, the CIL enum field names are used after conforming them to protobuf conventions

License
-------
This project is subject to the terms of the [Mozilla Public License, v. 2.0](./LICENSE).