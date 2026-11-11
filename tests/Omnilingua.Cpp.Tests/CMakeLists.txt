add_executable(Omnilingua.Cpp.Tests main.cpp MappingTests.cpp)
target_link_libraries(Omnilingua.Cpp.Tests PRIVATE Omnilingua::Cpp)
add_test(NAME Omnilingua.Cpp.Tests COMMAND Omnilingua.Cpp.Tests)
