Memcached Take-home 
Assignment 

Memcached is an in-memory key-value store widely used by backend developers for storing 
and caching small chunks of data from database calls, API calls, or page renders. This exercise 
asks you to implement a simplified version of a memcached server in C#. 
This assignment assumes you have experience with network programming, implementing TCP 
clients and servers, and basic familiarity with memcached. 

## Requirements 
● Implement a TCP server in C# with a command-line interface 
● The server should speak a limited subset of the memcached text protocol: 
    ○ Implement only get, set, and delete commands 
    ○ See https://github.com/memcached/memcached/blob/master/doc/protocol.txt 
    ○ CAS-related parameters and values can be ignored and/or set to 0 
    ○ Authentication handling is not needed 
● Support for multiple concurrent clients 
● Implement a basic eviction policy for keys (choose and justify a policy of your choice) 
● Limits 
    ○ Key: 250 bytes 
    ○ Value: 102400 bytes (100 kB) 
    ○ Max number of keys: 3000 
    ○ Max total size of all values: 1073741824 bytes (1 GB) 
    ○ Server should reject commands exceeding these limits unless key eviction is 
    applicable 

## Time Expectation 
We expect you to spend around 4-6 hours on this assignment. If you're unable to complete all 
requirements within this timeframe, that's fine. Please document your approach and what you 
would do with more time. 
Use of AI and LLMs 
You may use ChatGPT, Claude, or similar services to help with research, just as you would use 
search engines. However, all code must be written by you, so we expect that you will be 
able to explain the thinking behind it. Please do not post or share this document or its 
contents with others or with other services as it is considered confidential. . 

## Evaluation Criteria 
After submission, we will evaluate your code based on the following criteria. Weight in 
percentage. 
● Correctness & Protocol Implementation (25%) 
● Architecture, Code Quality & Documentation (25%) 
● Performance & Resource Management (20%) 
● Concurrency & Error Handling (20%) 
● Security & Testing (10%) 

## Notes 
● If you require any accommodation to complete this, please let your recruiter know 
● Submit the code as a .zip file that includes your name 
● Feel free to make reasonable assumptions about unspecified details, but document 
them 
● If you advance to the next interview stage, we will conduct a walkthrough of your code 
where you'll explain your implementation and reasoning behind key design decisions 