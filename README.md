# cron-yaml

Simple cron-like execution of apps based on YAML input (Based on cron-xml with some improvements)

Use like this: cron-yaml myFile.yaml

Example yaml file:
```yaml
- Name: group1
  Active: true
  Job:
    - Name: job1
      Active: true
      Minutely: 1
      Task:
        - Name: task1
          Active: true
          FileName: 'C:\hello-world.exe'
          Arguments: 'ARG1'
          WorkingDirectory: 'C:\Users\jayab'
          MaxLogLines: 4
        - Name: task2
          Active: true
          FileName: 'C:\hello-world.exe'
          Arguments: 'ARG1 ARG2'
          WorkingDirectory: 'C:\'
          MaxLogLines: 8
        - Name: task3
          FileName: 'C:\hello-world.exe'
          Arguments: 'ARG1 ARG2 ARG3'
          WorkingDirectory: 'C:\'
          MaxLogLines: 16
- Name: group2
  Active: true
  Job:
    - Name: job2
      Active: true
      Hourly: 1
      TimeZone: 'Mountain Standard Time'
      Task:
        - Name: task4
          Active: true
          FileName: 'C:\hello-world.exe'
          Arguments: 'ARRRG'
          WorkingDirectory: 'C:\'
          MaxLogLines: 15
```

- Jobs and tasks will be executed sequentially, while groups will be executed concurrently.
- In case Minutely, Hourly, or Daily values are not provided, the default value will be Daily with a value of 1.
- To limit the size of a task's StdOut, you can use MaxLogLines (default 1000), which will truncate the oldest lines first.
- You can deactivate a group, job, or task by setting the Active parameter to false.
- You can adjust the TimeZone for a job to display an appropriate timestamp in the log files.





