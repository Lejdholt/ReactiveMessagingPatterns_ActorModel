using System;
using Akka.Actor;
using Akka.Event;
using ReactiveMessagingPatterns.ActorModel;
using ReactiveMessagingPatterns.ActorModel.co.vaughnvernon.reactiveenterprise;
using Xunit;
using Xunit.Abstractions;

namespace ReactiveMessagingPatterns.ActorModel1.co.vaughnvernon.reactiveenterprise.routingslip
{
    public class CustomerInformation : NicePrint
    {
        public string Name { get; }
        public string FederalTaxId { get; }

        public CustomerInformation(string name, string federalTaxId)
        {
            Name = name;
            FederalTaxId = federalTaxId;
        }
    }

    public class ContactInformation:NicePrint
    {
        public PostalAddress PostalAddress { get; }
        public Telephone Telephone { get; }

        public ContactInformation(PostalAddress postalAddress, Telephone telephone)
        {
            PostalAddress = postalAddress;
            Telephone = telephone;
        }
    }

    public class PostalAddress : NicePrint
    {
        public string Address1 { get; }
        public string Address2 { get; }
        public string City { get; }
        public string State { get; }
        public string ZipCode { get; }

        public PostalAddress(string address1, string address2, string city, string state, string zipCode)
        {
            Address1 = address1;
            Address2 = address2;
            City = city;
            State = state;
            ZipCode = zipCode;
        }
    }

    public class Telephone : NicePrint
    {
        public string Number { get; }

        public Telephone(string number)
        {
            Number = number;
        }
    }

    public class ServiceOption : NicePrint
    {
        public string Id { get; }
        public string Description { get; }

        public ServiceOption(string id, string description)
        {
            Id = id;
            Description = description;
        }
    }

    public class RegistrationData
    {
        public CustomerInformation CustomerInformation { get; }
        public ContactInformation ContactInformation { get; }
        public ServiceOption ServiceOption { get; }

        public RegistrationData(CustomerInformation customerInformation, ContactInformation contactInformation, ServiceOption serviceOption)
        {
            CustomerInformation = customerInformation;
            ContactInformation = contactInformation;
            ServiceOption = serviceOption;
        }
    }

    public class ProcessStep
    {
        public string Name { get; }
        public IActorRef Processor { get; }

        public ProcessStep(string name, IActorRef processor)
        {
            Name = name;
            Processor = processor;
        }
    }

    public class RegistrationProcess
    {
        public string ProcessId { get; }
        public ProcessStep[] ProcessSteps { get; }
        public int CurrentStep { get; }

        public RegistrationProcess(string processId, ProcessStep[] processSteps, int currentStep)
        {
            ProcessId = processId;
            ProcessSteps = processSteps;
            CurrentStep = currentStep;
        }

        public RegistrationProcess(string processId, ProcessStep[] processSteps) : this(processId, processSteps, 0)
        {
        }

        public bool IsCompleted() => CurrentStep >= ProcessSteps.Length;

        public ProcessStep NextStep()
        {
            if (IsCompleted())
            {
                throw new Exception("Process had already completed.");
            }

            return ProcessSteps[CurrentStep];
        }

        public RegistrationProcess StepCompleted()
        {
            return new RegistrationProcess(ProcessId, ProcessSteps, CurrentStep + 1);
        }
    }

    public class RegisterCustomer
    {
        public RegistrationData RegistrationData { get; }
        public RegistrationProcess RegistrationProcess { get; }

        public RegisterCustomer(RegistrationData registrationData, RegistrationProcess registrationProcess)
        {
            RegistrationData = registrationData;
            RegistrationProcess = registrationProcess;
        }

        public void Advance()
        {
            var advancedProcess = RegistrationProcess.StepCompleted();

            if (!advancedProcess.IsCompleted())
            {
                advancedProcess.NextStep().Processor.Tell(new RegisterCustomer(RegistrationData, advancedProcess));
            }
            RoutingSlipDriver.CompletedStep();
        }
    }

    public class RoutingSlipDriver : CompletableApp
    {
        public RoutingSlipDriver(ITestOutputHelper helper) : base(4, helper)
        {
         
        }

        [Fact]
        public void Main()
        {
            var processId = Guid.NewGuid().ToString();

            var step1 = new ProcessStep("create_customer", ServiceRegistry.CustomerVault(Sys, processId));
            var step2 = new ProcessStep("set_up_contact_info", ServiceRegistry.ContactKeeper(Sys, processId));
            var step3 = new ProcessStep("select_service_plan", ServiceRegistry.ServicePlanner(Sys, processId));
            var step4 = new ProcessStep("check_credit", ServiceRegistry.CreditChecker(Sys, processId));

            var registrationProcess = new RegistrationProcess(processId, new[] { step1, step2, step3, step4 });

            var registrationData =
                new RegistrationData(
                    new CustomerInformation("ABC, Inc.", "123-45-6789"),
                    new ContactInformation(
                        new PostalAddress("123 Main Street", "Suite 100", "Boulder", "CO", "80301"),
                        new Telephone("303-555-1212")),
                    new ServiceOption("99-1203", "A description of 99-1203."));

            var registerCustomer = new RegisterCustomer(registrationData, registrationProcess);

            registrationProcess.NextStep().Processor.Tell(registerCustomer);

            AwaitCompletion();
            helper.WriteLine("RoutingSlip: is completed.");
        }
    }

    class CreditChecker : ReceiveActor
    {
        public CreditChecker()
        {
            log = Context.GetLogger();
            Receive<RegisterCustomer>(registerCustomer =>
            {
                var federalTaxId = registerCustomer.RegistrationData.CustomerInformation.FederalTaxId;

                log.Info($"CreditChecker: handling register customer to perform credit check: {federalTaxId}");
                registerCustomer.Advance();
                Context.Stop(Self);
            });
            ReceiveAny(message => log.Info($"CreditChecker: received unexpected message: {message.ToString()}"));
        }

        private ILoggingAdapter log;
    }

    class ContactKeeper : ReceiveActor
    {
        public ContactKeeper()
        {
            log = Context.GetLogger();
            Receive<RegisterCustomer>(registerCustomer =>
            {
                var contactInfo = registerCustomer.RegistrationData.ContactInformation;
                log.Info($"ContactKeeper: handling register customer to keep contact information: {contactInfo}");
                registerCustomer.Advance();
                Context.Stop(Self);
            });

            ReceiveAny(message => log.Info($"ContactKeeper: received unexpected message: {message.ToString()}"));
        }

        private ILoggingAdapter log;

     
    }

    class CustomerVault : ReceiveActor
    {
        public CustomerVault()
        {
            log = Context.GetLogger();
            Receive<RegisterCustomer>(registerCustomer =>
            {
                var customerInformation = registerCustomer.RegistrationData.CustomerInformation;

                log.Info($"CustomerVault: handling register customer to create a new customer: {customerInformation}");

                registerCustomer.Advance();
                Context.Stop(Self);
            });
            ReceiveAny(message => log.Info($"CustomerVault: received unexpected message: {message.ToString()}"));
        }

        private ILoggingAdapter log;
    }

    class ServicePlanner : ReceiveActor
    {
        public ServicePlanner()
        {
            log = Context.GetLogger();
            Receive<RegisterCustomer>(registerCustomer =>
            {
                var serviceOption = registerCustomer.RegistrationData.ServiceOption;

                log.Info($"ServicePlanner: handling register customer to plan a new customer service: {serviceOption}");

                registerCustomer.Advance();
                Context.Stop(Self);
            });
            ReceiveAny(message => log.Info($"CustomerVault: received unexpected message: {message.ToString()}"));
        }

        private ILoggingAdapter log;
    }

    public class ServiceRegistry
    {
        public static IActorRef ContactKeeper(ActorSystem system, string id) => system.ActorOf(Props.Create<ContactKeeper>(), "contactKeeper-" + id);
        public static IActorRef CreditChecker(ActorSystem system, string id) => system.ActorOf(Props.Create<CreditChecker>(), "creditChecker-" + id);
        public static IActorRef CustomerVault(ActorSystem system, string id) => system.ActorOf(Props.Create<CustomerVault>(), "customerVault-" + id);
        public static IActorRef ServicePlanner(ActorSystem system, string id) => system.ActorOf(Props.Create<ServicePlanner>(), "servicePlanner-" + id);
    }
}