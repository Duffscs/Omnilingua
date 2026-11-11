cmake_minimum_required(VERSION 3.24)
project(OmnilinguaCpp VERSION 9.9.9 LANGUAGES NONE)
install(FILES OmnilinguaCppConfig.cmake DESTINATION share/OmnilinguaCpp)
install(DIRECTORY include generator cmake DESTINATION share/OmnilinguaCpp
    PATTERN "__pycache__" EXCLUDE PATTERN "*.pyc" EXCLUDE)
