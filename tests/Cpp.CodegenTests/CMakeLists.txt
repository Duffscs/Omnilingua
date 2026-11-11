add_executable(Cpp.CodegenTests main.cpp)
target_link_libraries(Cpp.CodegenTests PRIVATE Omnilingua::Cpp)
omnilingua_generate_contracts(Cpp.CodegenTests Plugins.h)
add_test(NAME Cpp.CodegenTests COMMAND Cpp.CodegenTests)
