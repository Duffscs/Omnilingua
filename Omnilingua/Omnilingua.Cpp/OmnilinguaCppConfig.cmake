include_guard(GLOBAL)
include(CMakeFindDependencyMacro)
find_dependency(Threads)
set(OMNILINGUA_CPP_SDK_ROOT "${CMAKE_CURRENT_LIST_DIR}")
if(NOT TARGET Omnilingua::Cpp)
    add_library(Omnilingua::Cpp INTERFACE IMPORTED GLOBAL)
    set_target_properties(Omnilingua::Cpp PROPERTIES
        INTERFACE_INCLUDE_DIRECTORIES "${OMNILINGUA_CPP_SDK_ROOT}/include"
        INTERFACE_COMPILE_FEATURES cxx_std_20
        INTERFACE_LINK_LIBRARIES Threads::Threads)
    if(MSVC)
        set_property(TARGET Omnilingua::Cpp PROPERTY INTERFACE_COMPILE_OPTIONS /utf-8)
    endif()
endif()
include("${CMAKE_CURRENT_LIST_DIR}/cmake/OmnilinguaCppGenerate.cmake")
